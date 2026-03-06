using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using NetCord.Gateway.Voice.BinaryModels;
using NetCord.Gateway.Voice.Encryption;
using NetCord.Gateway.Voice.JsonModels;
using NetCord.Gateway.Voice.UdpSockets;
using NetCord.Gateway.WebSockets;
using NetCord.Logging;

using WebSocketCloseStatus = System.Net.WebSockets.WebSocketCloseStatus;

namespace NetCord.Gateway.Voice;

public sealed partial class VoiceClient : WebSocketClient
{
    private class VoiceState(VoiceClient client) : State
    {
        public override void Abort()
        {
            if (client._udpState is { } udpState && udpState.TryIndicateAborting())
                udpState.Connection.Abort();
        }
    }

    internal class UdpState(IUdpConnection connection, IVoiceEncryption encryption, DaveSession daveSession) : IDisposable
    {
        public IUdpConnection Connection => connection;
        public IVoiceEncryption Encryption => encryption;
        public DaveSession DaveSession => daveSession;

        private CancellationTokenProvider? _closedTokenProvider;

        public bool TryIndicateConnecting(out CancellationToken closedCancellationToken)
        {
            CancellationTokenProvider closedTokenProvider = new();
            if (Interlocked.CompareExchange(ref _closedTokenProvider, closedTokenProvider, null) is not null)
            {
                closedTokenProvider.Dispose();
                closedCancellationToken = default;
                return false;
            }

            closedCancellationToken = closedTokenProvider.Token;
            return true;
        }

        public bool TryIndicateAborting()
        {
            CancellationTokenProvider? closedTokenProvider = Interlocked.Exchange(ref _closedTokenProvider, null);
            if (closedTokenProvider is null)
                return false;

            closedTokenProvider.Cancel();
            return true;
        }

        public void Dispose()
        {
            connection.Dispose();
            encryption.Dispose();
            daveSession.Dispose();
            _closedTokenProvider?.Dispose();
        }
    }

    private static readonly Dave.MlsFailureCallback s_mlsFailureCallback = LogMlsFailure;

    internal readonly struct DaveEncryptor(Dave.EncryptorHandle encryptor)
    {
        public unsafe Dave.EncryptorResultCode Encrypt(Dave.MediaType mediaType, uint ssrc, ReadOnlySpan<byte> frame, Span<byte> encryptedFrame, out int bytesWritten)
        {
            var result = Dave.EncryptorEncrypt(encryptor, mediaType, ssrc, frame, (uint)frame.Length, encryptedFrame, (nuint)encryptedFrame.Length, out var rawBytesWritten);
            bytesWritten = (int)rawBytesWritten;
            return result;
        }

        public int GetMaxCiphertextSize(Dave.MediaType mediaType, int plaintextByteSize)
            => (int)Dave.EncryptorGetMaxCiphertextByteSize(encryptor, mediaType, (nuint)plaintextByteSize);
    }

    internal readonly struct DaveDecryptor(Dave.DecryptorHandle decryptor)
    {
        public unsafe Dave.DecryptorResultCode Decrypt(Dave.MediaType mediaType, uint ssrc, ReadOnlySpan<byte> encryptedFrame, Span<byte> frame, out int bytesWritten)
        {
            var result = Dave.DecryptorDecrypt(decryptor, mediaType, encryptedFrame, (nuint)encryptedFrame.Length, frame, (nuint)frame.Length, out var rawBytesWritten);
            bytesWritten = (int)rawBytesWritten;
            return result;
        }

        public int GetMaxPlaintextByteSize(Dave.MediaType mediaType, int ciphertextByteSize)
            => (int)Dave.DecryptorGetMaxPlaintextByteSize(decryptor, mediaType, (nuint)ciphertextByteSize);
    }

    internal sealed class DaveSession : IDisposable
    {
        private const int MaxSnowflakeCStringSize = 21;
        private const int MlsNewGroupExpectedEpoch = 1;

        private static readonly Dave.LogSinkCallback s_logSinkCallback = LogSink;

        private ushort _latestPreparedTransitionVersion;

        private readonly Dictionary<ushort, ushort> _transitions = [];
        private readonly Dave.EncryptorHandle _encryptor = Dave.EncryptorCreate();
        private readonly ConcurrentDictionary<uint, Dave.DecryptorHandle> _decryptors = [];
        private readonly VoiceClient _client;
        private readonly Dave.SessionHandle _session;

        static DaveSession()
        {
            Dave.SetLogSinkCallback(s_logSinkCallback);
        }

        public DaveSession(VoiceClient client, Dave.MlsFailureCallback mlsFailureCallback, IntPtr userData)
        {
            _client = client;
            _session = Dave.SessionCreate(IntPtr.Zero, default, mlsFailureCallback, userData);
        }

        internal bool IsEnabled => GetProtocolVersion() is not Dave.DisabledVersion;

        internal DaveEncryptor Encryptor => new(_encryptor);
        internal VoiceClient Client => _client;

        internal static ushort GetMaxSupportedProtocolVersion()
        {
            return Dave.MaxSupportedProtocolVersion();
        }

        private ushort GetProtocolVersion()
        {
            return Dave.SessionGetProtocolVersion(_session);
        }

        internal DaveDecryptor? GetDecryptor(uint ssrc)
        {
            return _decryptors.TryGetValue(ssrc, out var decryptor) ? new(decryptor) : null;
        }

        internal void OnSpeaking(ulong userId, uint ssrc)
        {
            SetupKeyRatchetForUser(userId, ssrc, _latestPreparedTransitionVersion);
        }

        internal void OnClientDisconnect(ulong userId)
        {
            if (_client.Cache.UserSsrcs.TryGetValue(userId, out var ssrc)
                && _decryptors.TryRemove(ssrc, out var decryptor))
                decryptor.Dispose();
        }

        internal ValueTask OnSessionDescriptionAsync(ConnectionState connectionState, ushort protocolVersion)
        {
            return HandleInitAsync(connectionState, protocolVersion);
        }

        internal ValueTask OnPrepareTransitionAsync(ConnectionState connectionState, ushort transitionId, ushort protocolVersion)
        {
            PrepareRatchets(transitionId, protocolVersion);
            if (transitionId is not Dave.InitTransitionId)
            {
                SetDecryptorsPassthroughMode(protocolVersion is Dave.DisabledVersion);
                return SendTransitionReadyAsync(connectionState, transitionId);
            }

            return default;
        }

        internal void OnExecuteTransition(ushort transitionId)
        {
            HandleExecuteTransition(transitionId);
        }

        internal ValueTask OnPrepareEpoch(ConnectionState connectionState, int epoch, ushort protocolVersion)
        {
            if (epoch is MlsNewGroupExpectedEpoch)
            {
                InitSession(protocolVersion);
                return SendMlsKeyPackageAsync(connectionState);
            }

            return default;
        }

        internal void OnMlsExternalSender(ReadOnlySpan<byte> externalSender)
        {
            Dave.SessionSetExternalSender(_session, externalSender, (nuint)externalSender.Length);
        }

        internal ValueTask OnMlsProposalsAsync(ConnectionState connectionState, ReadOnlySpan<byte> proposals)
        {
            var recognizedUserIds = GetRecognizedUserIds(out var buffer);

            Dave.SessionProcessProposals(_session, proposals, (nuint)proposals.Length, recognizedUserIds, (nuint)recognizedUserIds.Length, out var commitWelcome, out var commitWelcomeLength);
            using (commitWelcome)
            {
                FreeRecognizedUserIdsBuffer(buffer);

                if (commitWelcome is not null && !commitWelcome.IsInvalid)
                    return SendMlsCommitWelcomeAsync(connectionState, commitWelcome.AsSpan((int)commitWelcomeLength));

                _client.Log<object?>(LogLevel.Debug, null, null, static (s, e) => "Dave proposals produced no commit-welcome payload.");

                return default;
            }
        }

        internal ValueTask OnMlsPrepareCommitTransitionAsync(ConnectionState connectionState, ushort transitionId, ReadOnlySpan<byte> commit)
        {
            using var commitResultHandle = Dave.SessionProcessCommit(_session, commit, (nuint)commit.Length);

            if (commitResultHandle is null || commitResultHandle.IsInvalid)
                return HandleRosterUpdatedAsync(connectionState, transitionId, true);

            if (Dave.CommitResultIsIgnored(commitResultHandle))
                return default;

            return HandleRosterUpdatedAsync(connectionState, transitionId, Dave.CommitResultIsFailed(commitResultHandle));
        }

        internal ValueTask OnMlsWelcomeAsync(ConnectionState connectionState, ushort transitionId, ReadOnlySpan<byte> welcome)
        {
            var recognizedUserIds = GetRecognizedUserIds(out var buffer);

            using var welcomeResult = Dave.SessionProcessWelcome(_session, welcome, (nuint)welcome.Length, recognizedUserIds, (nuint)recognizedUserIds.Length);

            FreeRecognizedUserIdsBuffer(buffer);

            return HandleRosterUpdatedAsync(connectionState, transitionId, welcomeResult is null || welcomeResult.IsInvalid);
        }

        private static void LogSink(Dave.LoggingSeverity severity, IntPtr file, int line, IntPtr message)
        {
#if DEBUG
            var fileString = PointerToUtf8String(file);
            var messageString = PointerToUtf8String(message);
            System.Diagnostics.Debug.WriteLine($"Dave at {fileString}:{line}: {messageString}");
#endif
        }

        private static string? PointerToUtf8String(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
                return null;

            int length = 0;
            while (Marshal.ReadByte(pointer, length) is not 0)
                length++;

            byte[] buffer = new byte[length];
            Marshal.Copy(pointer, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        private void SetDecryptorsPassthroughMode(bool passthroughMode)
        {
            foreach (var decryptor in _decryptors.Values)
                Dave.DecryptorTransitionToPassthroughMode(decryptor, passthroughMode);
        }

        private async ValueTask HandleRosterUpdatedAsync(ConnectionState connectionState, ushort transitionId, bool isFailed)
        {
            if (!isFailed)
            {
                PrepareRatchets(transitionId, GetProtocolVersion());
                if (transitionId is not Dave.InitTransitionId)
                    await SendTransitionReadyAsync(connectionState, transitionId).ConfigureAwait(false);
            }
            else
            {
                await SendMlsInvalidCommitWelcomeAsync(connectionState, transitionId).ConfigureAwait(false);
                await SendMlsKeyPackageAsync(connectionState).ConfigureAwait(false);
            }
        }

        private unsafe ReadOnlySpan<nint> GetRecognizedUserIds(out nint[] pointers)
        {
            var users = _client.Cache.Users;
            int count = users.Count + 1;

            pointers = ArrayPool<nint>.Shared.Rent(count);

            var buffer = (byte*)Marshal.AllocHGlobal(count * MaxSnowflakeCStringSize);
            var result = pointers.AsSpan(0, count);

            int i = 0;
            int written = 0;
            foreach (var userId in users)
                AddUserId(ref result[i++], ref written, buffer, userId);

            AddUserId(ref result[i], ref written, buffer, _client.UserId);
            return result;

            static void AddUserId(ref nint pointer, ref int written, byte* buffer, ulong userId)
            {
                var start = buffer + written;
                var writtenSpan = SnowflakeToCString(userId, new(start, MaxSnowflakeCStringSize));
                pointer = (nint)start;
                written += writtenSpan.Length;
            }
        }

        private static unsafe void FreeRecognizedUserIdsBuffer(nint[] recognizedUserIds)
        {
            var buffer = (byte*)recognizedUserIds[0];
            Marshal.FreeHGlobal((nint)buffer);
            ArrayPool<nint>.Shared.Return(recognizedUserIds);
        }

        private ValueTask HandleInitAsync(ConnectionState connectionState, ushort protocolVersion)
        {
            InitSession(protocolVersion);

            if (protocolVersion > Dave.DisabledVersion)
                return SendMlsKeyPackageAsync(connectionState);

            PrepareRatchets(Dave.InitTransitionId, protocolVersion);
            HandleExecuteTransition(Dave.InitTransitionId);
            return default;
        }

        private void HandleExecuteTransition(ushort transitionId)
        {
            if (!_transitions.Remove(transitionId, out var protocolVersion))
                return;

            if (protocolVersion is Dave.DisabledVersion)
                Dave.SessionReset(_session);

            SetupEncryptionKeyRatchet(protocolVersion);
        }

        private void PrepareRatchets(ushort transitionId, ushort protocolVersion)
        {
            foreach (var pair in _client.Cache.UserSsrcs)
                SetupKeyRatchetForUser(pair.Key, pair.Value, protocolVersion);

            if (transitionId is Dave.InitTransitionId)
                SetupEncryptionKeyRatchet(protocolVersion);
            else
                _transitions[transitionId] = protocolVersion;

            _latestPreparedTransitionVersion = protocolVersion;
        }

        private void SetupKeyRatchetForUser(ulong userId, uint ssrc, ushort protocolVersion)
        {
            using var keyRatchet = GetUserKeyRatchet(userId, protocolVersion);

            var decryptor = _decryptors.GetOrAdd(ssrc, _ =>
            {
                var created = Dave.DecryptorCreate();
                Dave.DecryptorTransitionToPassthroughMode(created, protocolVersion is Dave.DisabledVersion);
                return created;
            });

            if (keyRatchet is not null)
                Dave.DecryptorTransitionToKeyRatchet(decryptor, keyRatchet);
        }

        private void SetupEncryptionKeyRatchet(ushort protocolVersion)
        {
            using var keyRatchet = GetUserKeyRatchet(_client.UserId, protocolVersion);
            if (keyRatchet is not null)
                Dave.EncryptorSetKeyRatchet(_encryptor, keyRatchet);
        }

        [SkipLocalsInit]
        private Dave.KeyRatchetHandle? GetUserKeyRatchet(ulong userId, ushort protocolVersion)
        {
            if (protocolVersion is Dave.DisabledVersion)
                return null;

            Span<byte> byteUserId = stackalloc byte[MaxSnowflakeCStringSize];
            byteUserId = SnowflakeToCString(userId, byteUserId);
            return Dave.SessionGetKeyRatchet(_session, byteUserId);
        }

        private async ValueTask SendMlsKeyPackageAsync(ConnectionState connectionState)
        {
            byte[]? payload = null;
            int payloadLength;
            Dave.SessionGetMarshalledKeyPackage(_session, out var keyPackage, out var length);
            using (keyPackage)
            {
                if (keyPackage is null || keyPackage.IsInvalid || length == 0)
                {
                    _client.Log<object?>(LogLevel.Warning, null, null, static (s, e) => "Dave returned no MLS key package.");
                    return;
                }

                payloadLength = (int)length + 1;
                payload = ArrayPool<byte>.Shared.Rent(payloadLength);
                keyPackage.AsSpan((int)length).CopyTo(payload.AsSpan(1));
            }

            payload[0] = (byte)VoiceOpcode.DaveMlsKeyPackage;

            try
            {
                await _client.SendConnectionPayloadAsync(connectionState, payload.AsMemory(0, payloadLength), _client._internalBinaryPayloadProperties).ConfigureAwait(false);
            }
            finally
            {
                if (payload is not null)
                    ArrayPool<byte>.Shared.Return(payload);
            }
        }

        private ValueTask SendTransitionReadyAsync(ConnectionState connectionState, ushort transitionId)
        {
            VoicePayloadProperties<DaveTransitionReadyProperties> readyPayload = new(VoiceOpcode.DaveTransitionReady, new(transitionId));
            return _client.SendConnectionPayloadAsync(connectionState, readyPayload.Serialize(Serialization.Default.VoicePayloadPropertiesDaveTransitionReadyProperties), _client._internalTextPayloadProperties);
        }

        private ValueTask SendMlsCommitWelcomeAsync(ConnectionState connectionState, ReadOnlySpan<byte> commitWelcomeMessage)
        {
            int payloadLength = commitWelcomeMessage.Length + 1;
            var payload = ArrayPool<byte>.Shared.Rent(payloadLength);

            commitWelcomeMessage.CopyTo(payload.AsSpan(1));
            payload[0] = (byte)VoiceOpcode.DaveMlsCommitWelcome;

            return ContinueAsync(_client, connectionState, payload, payloadLength);

            static async ValueTask ContinueAsync(VoiceClient client, ConnectionState connectionState, byte[] payload, int payloadLength)
            {
                try
                {
                    await client.SendConnectionPayloadAsync(connectionState, payload.AsMemory(0, payloadLength), client._internalBinaryPayloadProperties).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(payload);
                }
            }
        }

        private ValueTask SendMlsInvalidCommitWelcomeAsync(ConnectionState connectionState, ushort transitionId)
        {
            VoicePayloadProperties<DaveMlsInvalidCommitWelcomeProperties> invalidPayload = new(VoiceOpcode.DaveMlsInvalidCommitWelcome, new(transitionId));
            return _client.SendConnectionPayloadAsync(connectionState, invalidPayload.Serialize(Serialization.Default.VoicePayloadPropertiesDaveMlsInvalidCommitWelcomeProperties), _client._internalTextPayloadProperties);
        }

        [SkipLocalsInit]
        private void InitSession(ushort protocolVersion)
        {
            Span<byte> userId = stackalloc byte[MaxSnowflakeCStringSize];
            userId = SnowflakeToCString(_client.UserId, userId);
            Dave.SessionInit(_session, protocolVersion, _client.ChannelId, userId);
        }

        private static Span<byte> SnowflakeToCString(ulong snowflake, Span<byte> buffer)
        {
            Span<char> chars = stackalloc char[20];
            if (!snowflake.TryFormat(chars, out int charsWritten))
                ThrowFailedToFormatSnowflake();

            int bytesWritten = Encoding.UTF8.GetBytes(chars[..charsWritten], buffer);
            buffer[bytesWritten] = 0;
            return buffer[..(bytesWritten + 1)];
        }

        [DoesNotReturn]
        private static void ThrowFailedToFormatSnowflake()
        {
            throw new InvalidOperationException("Failed to format snowflake.");
        }

        public void Dispose()
        {
            _session.Dispose();
            _encryptor.Dispose();
            foreach (var decryptor in _decryptors.Values)
                decryptor.Dispose();
        }
    }

    public partial event Func<VoiceReceiveEventArgs, ValueTask>? VoiceReceive;
    public partial event Func<ValueTask>? Ready;
    public partial event Func<SpeakingEventArgs, ValueTask>? Speaking;
    public partial event Func<UserConnectEventArgs, ValueTask>? UserConnect;
    public partial event Func<UserDisconnectEventArgs, ValueTask>? UserDisconnect;

    public ulong UserId { get; }

    public string SessionId { get; }

    public string Endpoint { get; }

    public ulong GuildId { get; }

    public ulong ChannelId { get; }

    public string Token { get; }

    /// <summary>
    /// The cache of the <see cref="VoiceClient"/>.
    /// </summary>
    public IVoiceClientCache Cache { get; private set; }

    /// <summary>
    /// The sequence number of the <see cref="VoiceClient"/>.
    /// </summary>
    public int SequenceNumber { get; private set; } = -1;

    private protected override Uri Uri { get; }

    private readonly IUdpConnectionProvider _udpConnectionProvider;
    private readonly IVoiceEncryptionProvider _encryptionProvider;
    private readonly IVoiceReceiveHandler _receiveHandler;
    private readonly TimeSpan _externalSocketAddressDiscoveryTimeout;
    private readonly GCHandle _loggerHandle;

    internal UdpState? _udpState;

    public VoiceClient(ulong userId, string sessionId, string endpoint, ulong guildId, string token, VoiceClientConfiguration? configuration = null)
        : this(userId, sessionId, endpoint, guildId, 0, token, configuration)
    {
    }

    public VoiceClient(ulong userId, string sessionId, string endpoint, ulong guildId, ulong channelId, string token, VoiceClientConfiguration? configuration = null) : base(configuration ??= new())
    {
        UserId = userId;
        SessionId = sessionId;
        Uri = new($"wss://{Endpoint = endpoint}?v={(int)configuration.Version.GetValueOrDefault(VoiceApiVersion.V8)}", UriKind.Absolute);
        GuildId = guildId;
        ChannelId = channelId;
        Token = token;

        var cacheProvider = configuration.CacheProvider ?? ImmutableVoiceClientCacheProvider.Empty;
        Cache = cacheProvider.Create();
        _udpConnectionProvider = configuration.UdpConnectionProvider ?? UdpConnectionProvider.Instance;
        _encryptionProvider = configuration.EncryptionProvider ?? VoiceEncryptionProvider.Instance;
        _receiveHandler = configuration.ReceiveHandler ?? NullVoiceReceiveHandler.Instance;
        _externalSocketAddressDiscoveryTimeout = configuration.ExternalSocketAddressDiscoveryTimeout.GetValueOrDefault(new(5 * TimeSpan.TicksPerSecond));
        _loggerHandle = GCHandle.Alloc(_logger);
    }

    private protected override ValueTask SendIdentifyAsync(ConnectionState connectionState, CancellationToken cancellationToken = default)
    {
        var serializedPayload = new VoicePayloadProperties<VoiceIdentifyProperties>(VoiceOpcode.Identify, new(GuildId, UserId, SessionId, Token, DaveSession.GetMaxSupportedProtocolVersion())).Serialize(Serialization.Default.VoicePayloadPropertiesVoiceIdentifyProperties);
        _latencyTimer.Start();
        return SendConnectionPayloadAsync(connectionState, serializedPayload, _internalTextPayloadProperties, cancellationToken);
    }

    private VoiceState CreateState()
    {
        return new(this);
    }

    /// <summary>
    /// Starts the <see cref="VoiceClient"/>.
    /// </summary>
    /// <returns></returns>
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        var connectionState = await StartAsync(CreateState(), cancellationToken).ConfigureAwait(false);

        Interlocked.Exchange(ref _udpState, null)?.Dispose();

        await SendIdentifyAsync(connectionState, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes the session.
    /// </summary>
    /// <param name="sequenceNumber">The sequence number of the payload to resume from.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns></returns>
    public async ValueTask ResumeAsync(int sequenceNumber, CancellationToken cancellationToken = default)
    {
        var connectionState = await StartAsync(CreateState(), cancellationToken).ConfigureAwait(false);

        if (_udpState is { } udpClient)
        {
            if (udpClient.TryIndicateConnecting(out var closedCancellationToken))
            {
                var connection = udpClient.Connection;

                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                _ = ReadAsync(connection, ArrayPool<byte>.Shared.Rent(ushort.MaxValue), closedCancellationToken);
            }

            await TryResumeAsync(connectionState, SequenceNumber = sequenceNumber, cancellationToken).ConfigureAwait(false);
        }
        else
            await SendIdentifyAsync(connectionState, cancellationToken).ConfigureAwait(false);
    }

    private protected override bool Reconnect(WebSocketCloseStatus? status, string? description)
    {
        return status is not (
            (WebSocketCloseStatus)4004 or
            (WebSocketCloseStatus)4006 or
            (WebSocketCloseStatus)4009 or
            (WebSocketCloseStatus)4011 or
            (WebSocketCloseStatus)4012 or
            (WebSocketCloseStatus)4014 or
            (WebSocketCloseStatus)4016 or
            (WebSocketCloseStatus)4021 or
            (WebSocketCloseStatus)4022
        );
    }

    private protected override ValueTask TryResumeAsync(ConnectionState connectionState, CancellationToken cancellationToken = default)
    {
        return TryResumeAsync(connectionState, SequenceNumber, cancellationToken);
    }

    private ValueTask TryResumeAsync(ConnectionState connectionState, int sequenceNumber, CancellationToken cancellationToken = default)
    {
        var serializedPayload = new VoicePayloadProperties<VoiceResumeProperties>(VoiceOpcode.Resume, new(GuildId, SessionId, Token, sequenceNumber)).Serialize(Serialization.Default.VoicePayloadPropertiesVoiceResumeProperties);
        _latencyTimer.Start();
        return SendConnectionPayloadAsync(connectionState, serializedPayload, _internalTextPayloadProperties, cancellationToken);
    }

    private protected override ValueTask HeartbeatAsync(ConnectionState connectionState, CancellationToken cancellationToken = default)
    {
        var serializedPayload = new VoicePayloadProperties<VoiceHeartbeatProperties>(VoiceOpcode.Heartbeat, new(Environment.TickCount, SequenceNumber)).Serialize(Serialization.Default.VoicePayloadPropertiesVoiceHeartbeatProperties);
        _latencyTimer.Start();
        return SendConnectionPayloadAsync(connectionState, serializedPayload, _internalTextPayloadProperties, cancellationToken);
    }

    private protected override ValueTask ProcessPayloadAsync(State state, ConnectionState connectionState, WebSocketMessageType messageType, ReadOnlySpan<byte> payload)
    {
        if (messageType is WebSocketMessageType.Text)
        {
            var jsonPayload = JsonSerializer.Deserialize(payload, Serialization.Default.JsonVoicePayload)!;
            return HandlePayloadAsync(state, connectionState, jsonPayload);
        }

        BinaryVoicePayload binaryPayload = new(payload);
        return HandleBinaryPayloadAsync(connectionState, binaryPayload);
    }

    private ValueTask HandleBinaryPayloadAsync(ConnectionState connectionState, BinaryVoicePayload payload)
    {
        SequenceNumber = payload.SequenceNumber;

        switch (payload.Opcode)
        {
            case VoiceOpcode.DaveMlsExternalSender:
                if (_udpState is { DaveSession: var externalSenderSession })
                    externalSenderSession.OnMlsExternalSender(payload.Data);
                return default;
            case VoiceOpcode.DaveMlsProposals:
                return _udpState is { DaveSession: var proposalsSession }
                    ? proposalsSession.OnMlsProposalsAsync(connectionState, payload.Data)
                    : default;
            case VoiceOpcode.DaveMlsAnnounceCommitTransition:
                if (_udpState is not { DaveSession: var commitSession })
                    return default;

                var commitData = payload.Data;
                var commitTransitionId = BinaryPrimitives.ReadUInt16BigEndian(commitData);
                return commitSession.OnMlsPrepareCommitTransitionAsync(connectionState, commitTransitionId, commitData[2..]);
            case VoiceOpcode.DaveMlsWelcome:
                if (_udpState is not { DaveSession: var welcomeSession })
                    return default;

                var welcomeData = payload.Data;
                var welcomeTransitionId = BinaryPrimitives.ReadUInt16BigEndian(welcomeData);
                return welcomeSession.OnMlsWelcomeAsync(connectionState, welcomeTransitionId, welcomeData[2..]);
            default:
                return default;
        }
    }

    private static void LogMlsFailure(IntPtr source, IntPtr reason, IntPtr userData)
    {
        try
        {
            if (GCHandle.FromIntPtr(userData).Target is IWebSocketLogger logger)
            {
                logger.Log(LogLevel.Error, (Source: (nint)source, Reason: (nint)reason), null, static (s, e) =>
                {
                    return $"An MLS error occurred: {PointerToUtf8String(s.Source)} {PointerToUtf8String(s.Reason)}";
                });
            }
        }
        catch
        {
        }

        static string? PointerToUtf8String(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
                return null;

            int length = 0;
            while (Marshal.ReadByte(pointer, length) is not 0)
                length++;

            byte[] buffer = new byte[length];
            Marshal.Copy(pointer, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }
    }

    private async ValueTask HandlePayloadAsync(State state, ConnectionState connectionState, JsonVoicePayload payload)
    {
        if (payload.SequenceNumber is int sequenceNumber)
            SequenceNumber = sequenceNumber;

        switch (payload.Opcode)
        {
            case VoiceOpcode.Ready:
                {
                    var latency = _latencyTimer.Elapsed;
                    var updateLatencyTask = UpdateLatencyAsync(latency).ConfigureAwait(false);
                    var ready = payload.Data.GetValueOrDefault().ToObject(Serialization.Default.JsonReady);

                    var (ip, port) = (ready.Ip, ready.Port);
                    var udpConnection = _udpConnectionProvider.CreateConnection(ip, port);
                    var encryption = _encryptionProvider.GetEncryption(ready.Modes);
                    UdpState newUdpState = new(udpConnection, encryption, new(this, s_mlsFailureCallback, GCHandle.ToIntPtr(_loggerHandle)));

                    if (Interlocked.CompareExchange(ref _udpState, newUdpState, null) is not null)
                    {
                        newUdpState.Dispose();
                        return;
                    }

                    if (!newUdpState.TryIndicateConnecting(out var closedCancellationToken))
                        return;

                    var ssrc = ready.Ssrc;
                    Cache = Cache.CacheCurrentSsrc(ssrc);

                    var encryptionName = encryption.Name;
                    Log(LogLevel.Debug, encryptionName, null, static (s, e) =>
                    {
                        return $"Using '{s}' encryption.";
                    });

                    await udpConnection.OpenAsync().ConfigureAwait(false);

                    var buffer = ArrayPool<byte>.Shared.Rent(ushort.MaxValue);

                    if (_receiveHandler.RequiresExternalSocketAddress)
                    {
                        Log<object?>(LogLevel.Debug, null, null, static (s, e) => "Getting external socket address.");

                        (ip, port) = await GetExternalSocketAddressAsync(udpConnection, ssrc, buffer).ConfigureAwait(false);
                        if (ip is null)
                        {
                            Log<object?>(LogLevel.Error, null, null, static (s, e) => "Failed to get the external socket address. Aborting the client.");

                            Abort();
                            return;
                        }

                        Log(LogLevel.Debug, (Ip: ip, Port: port), null, static (s, e) => $"External socket address: {s.Ip}:{s.Port}.");
                    }

                    _ = ReadAsync(udpConnection, buffer, closedCancellationToken);

                    Log<object?>(LogLevel.Debug, null, null, static (s, e) => "Selecting a protocol.");

                    VoicePayloadProperties<ProtocolProperties> protocolPayload = new(VoiceOpcode.SelectProtocol, new("udp", new(ip, port, encryptionName)));
                    await SendConnectionPayloadAsync(connectionState, protocolPayload.Serialize(Serialization.Default.VoicePayloadPropertiesProtocolProperties), _internalTextPayloadProperties).ConfigureAwait(false);

                    await updateLatencyTask;
                }
                break;
            case VoiceOpcode.SessionDescription:
                {
                    Log<object?>(LogLevel.Debug, null, null, static (s, e) => "Session description received.");

                    if (_udpState is not { Encryption: var encryption, DaveSession: var session })
                        return;

                    var sessionDescription = payload.Data.GetValueOrDefault().ToObject(Serialization.Default.JsonSessionDescription);
                    encryption.SetKey(sessionDescription.SecretKey);
                    await session.OnSessionDescriptionAsync(connectionState, sessionDescription.DaveProtocolVersion).ConfigureAwait(false);

                    Log<object?>(LogLevel.Information, null, null, static (s, e) => "Ready.");

                    var readyTask = InvokeEventAsync(_ready);

                    state.IndicateReady(connectionState);

                    await readyTask.ConfigureAwait(false);
                }
                break;
            case VoiceOpcode.Speaking:
                {
                    var json = payload.Data.GetValueOrDefault().ToObject(Serialization.Default.JsonSpeaking);
                    if (_udpState is { DaveSession: var session })
                        session.OnSpeaking(json.UserId, json.Ssrc);

                    await InvokeEventAsync(_speaking, this, json, static json => new SpeakingEventArgs(json), static (client, json) => client.Cache = client.Cache.CacheUserSsrc(json.UserId, json.Ssrc)).ConfigureAwait(false);
                }
                break;
            case VoiceOpcode.HeartbeatACK:
                {
                    var latency = _latencyTimer.Elapsed;

                    Log(LogLevel.Debug, latency, null, static (s, e) =>
                    {
                        return $"Heartbeat acknowledged after {s.TotalMilliseconds:F0} ms.";
                    });

                    await UpdateLatencyAsync(latency).ConfigureAwait(false);
                }
                break;
            case VoiceOpcode.Hello:
                {
                    Log<object?>(LogLevel.Debug, null, null, static (s, e) => "Hello received.");

                    StartHeartbeating(connectionState, payload.Data.GetValueOrDefault().ToObject(Serialization.Default.JsonHello).HeartbeatInterval);
                }
                break;
            case VoiceOpcode.Resumed:
                {
                    var latency = _latencyTimer.Elapsed;

                    Log<object?>(LogLevel.Information, null, null, static (s, e) => "Resumed.");

                    var updateLatencyTask = UpdateLatencyAsync(latency);
                    var resumeTask = InvokeResumeEventAsync();

                    state.IndicateReady(connectionState);

                    await resumeTask.ConfigureAwait(false);
                    await updateLatencyTask.ConfigureAwait(false);
                }
                break;
            case VoiceOpcode.ClientConnect:
                {
                    Log<object?>(LogLevel.Debug, null, null, static (s, e) => "Client connect received.");

                    var json = payload.Data.GetValueOrDefault().ToObject(Serialization.Default.JsonClientConnect);
                    await InvokeEventAsync(_userConnect, this, json, static json => new UserConnectEventArgs(json.UserIds), static (client, json) => client.Cache = client.Cache.CacheUsers(json.UserIds)).ConfigureAwait(false);
                }
                break;
            case VoiceOpcode.ClientDisconnect:
                {
                    Log<object?>(LogLevel.Debug, null, null, static (s, e) => "Client disconnect received.");

                    var json = payload.Data.GetValueOrDefault().ToObject(Serialization.Default.JsonClientDisconnect);
                    if (_udpState is { DaveSession: var session })
                        session.OnClientDisconnect(json.UserId);
                    await InvokeEventAsync(_userDisconnect, this, json, static json => new UserDisconnectEventArgs(json.UserId), static (client, json) => client.Cache = client.Cache.RemoveUser(json.UserId)).ConfigureAwait(false);
                }
                break;
            case VoiceOpcode.DavePrepareTransition:
                {
                    if (_udpState is not { DaveSession: var session })
                        return;

                    var prepareTransition = payload.Data.GetValueOrDefault().ToObject(Serialization.Default.JsonDavePrepareTransition);
                    await session.OnPrepareTransitionAsync(connectionState, prepareTransition.TransitionId, prepareTransition.ProtocolVersion).ConfigureAwait(false);
                }
                break;
            case VoiceOpcode.DaveExecuteTransition:
                {
                    if (_udpState is not { DaveSession: var session })
                        return;

                    var executeTransition = payload.Data.GetValueOrDefault().ToObject(Serialization.Default.JsonDaveExecuteTransition);
                    session.OnExecuteTransition(executeTransition.TransitionId);
                }
                break;
            case VoiceOpcode.DavePrepareEpoch:
                {
                    if (_udpState is not { DaveSession: var session })
                        return;

                    var prepareEpoch = payload.Data.GetValueOrDefault().ToObject(Serialization.Default.JsonDavePrepareEpoch);
                    await session.OnPrepareEpoch(connectionState, prepareEpoch.Epoch, prepareEpoch.ProtocolVersion).ConfigureAwait(false);
                }
                break;
        }
    }

    private async Task ReadAsync(IUdpConnection udpConnection, byte[] buffer, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                int length = await udpConnection.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                HandleDatagramReceive(new(buffer, 0, length));
            }
        }
        catch
        {
        }

        ArrayPool<byte>.Shared.Return(buffer);
    }

    private async ValueTask<(string? Ip, ushort Port)> GetExternalSocketAddressAsync(IUdpConnection udpConnection, uint ssrc, byte[] buffer)
    {
        var array = ArrayPool<byte>.Shared.Rent(74);

        var discoveryDatagram = CreateDiscoveryDatagram(array, ssrc);

        using CancellationTokenSource cancellationTokenSource = new(_externalSocketAddressDiscoveryTimeout);

        var cancellationToken = cancellationTokenSource.Token;

        int length;
        try
        {
            await udpConnection.SendAsync(discoveryDatagram, cancellationToken).ConfigureAwait(false);

            while (true)
            {
                length = await udpConnection.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (length is 74 && BinaryPrimitives.ReadUInt16BigEndian(buffer) is 2)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Log<object?>(LogLevel.Error, null, null, static (s, e) =>
            {
                return "Failed to get the external socket address due to timeout.";
            });
            return default;
        }
        catch (Exception ex)
        {
            Log<object?>(LogLevel.Error, null, ex, static (s, e) =>
            {
                return $"An error occurred while getting the external socket address.{Environment.NewLine}{e}";
            });
            return default;
        }

        ArrayPool<byte>.Shared.Return(array);

        return GetSocketAddress(buffer.AsSpan(0, length));
    }

    private static ReadOnlyMemory<byte> CreateDiscoveryDatagram(byte[] buffer, uint ssrc)
    {
        Memory<byte> bytes = new(buffer, 0, 74);
        var span = bytes.Span;
        BinaryPrimitives.WriteUInt16BigEndian(span, 1);
        BinaryPrimitives.WriteUInt16BigEndian(span[2..], 70);
        BinaryPrimitives.WriteUInt32BigEndian(span[4..], ssrc);
        span[8..].Clear();
        return bytes;
    }

    private static (string Ip, ushort Port) GetSocketAddress(ReadOnlySpan<byte> datagram)
    {
        var ip = Encoding.UTF8.GetString(datagram[8..72].TrimEnd((byte)0));
        var port = BinaryPrimitives.ReadUInt16BigEndian(datagram[72..]);
        return (ip, port);
    }

    private async void HandleDatagramReceive(ReadOnlyMemory<byte> datagram)
    {
        if (_udpState is not { Encryption: var encryption, DaveSession: var session })
            return;

        var handlers = _voiceReceive;
        if (handlers.IsEmpty)
            return;

        try
        {
            RtpPacketStorage packetStorage = new(datagram, encryption.ExtensionEncryption);

            var result = _receiveHandler.HandlePacket(this, GetPacketAndSsrc(packetStorage, out var ssrc));
            if (!result.Handle)
                return;

            var decryptor = session.GetDecryptor(ssrc);

            var framesMissed = result.FramesMissed;

            if (framesMissed is 0)
            {
                await InvokeEventForReceivedFrameAsync().ConfigureAwait(false);
                return;
            }

            var tasks = ArrayPool<ValueTask>.Shared.Rent(framesMissed + 1);

#pragma warning disable CA2012 // Use ValueTasks correctly
            for (ushort i = 0; i < framesMissed; i++)
            {
                tasks[i] = InvokeEventAsync(handlers, ssrc, static ssrc =>
                {
                    return new VoiceReceiveEventArgs(null, 0, 0, ssrc);
                }, nameof(_voiceReceive));
            }

            tasks[framesMissed] = InvokeEventForReceivedFrameAsync();
#pragma warning restore CA2012 // Use ValueTasks correctly

            await HandleTasksThatDoNotThrowAsync(tasks, framesMissed).ConfigureAwait(false);

            ValueTask InvokeEventForReceivedFrameAsync()
            {
                var eventArgs = CreateEventArgs((Encryption: encryption, PacketStorage: packetStorage, Ssrc: ssrc, Decryptor: decryptor, DaveEnabled: session.IsEnabled));
                if (eventArgs._buffer is null)
                    return default;

                return InvokeEventWithDisposalAsync(handlers, eventArgs, static args => args, static args =>
                {
                    if (args._buffer is not null)
                        ArrayPool<byte>.Shared.Return(args._buffer);
                }, nameof(_voiceReceive));

                VoiceReceiveEventArgs CreateEventArgs((IVoiceEncryption Encryption, RtpPacketStorage PacketStorage, uint Ssrc, DaveDecryptor? Decryptor, bool DaveEnabled) data)
                {
                    var packet = data.PacketStorage.Packet;
                    var encryption = data.Encryption;

                    var plaintextLength = packet.PayloadLength - encryption.Expansion;
                    var array = ArrayPool<byte>.Shared.Rent(plaintextLength);
                    var plaintext = array.AsSpan(0, plaintextLength);
                    encryption.Decrypt(packet, plaintext);

                    var extensionLength = packet.Extension
                        ? 4 * (encryption.ExtensionEncryption
                            ? BinaryPrimitives.ReadUInt16BigEndian(plaintext[2..]) + 1
                            : BinaryPrimitives.ReadUInt16BigEndian(packet.Datagram[(packet.HeaderLength + 2)..]))
                        : 0;

                    if (!data.DaveEnabled)
                        return new VoiceReceiveEventArgs(array, extensionLength, plaintextLength - extensionLength, data.Ssrc);

                    var decryptor = data.Decryptor;
                    if (decryptor is null)
                    {
                        Log<object?>(LogLevel.Debug, data.Ssrc, null, static (s, e) => $"Dropping voice frame because no Dave decryptor exists for SSRC {s}.");
                        ArrayPool<byte>.Shared.Return(array);
                        return default;
                    }

                    int paddingLength = packet.Padding
                        ? plaintext[^1]
                        : 0;

                    var ciphertext = plaintext[extensionLength..^paddingLength];
                    if (ciphertext.IsEmpty)
                    {
                        ArrayPool<byte>.Shared.Return(array);
                        return default;
                    }

                    int daveLength = decryptor.Value.GetMaxPlaintextByteSize(Dave.MediaType.Audio, ciphertext.Length);
                    byte[]? rentedDaveArray = null;
                    var daveArray = daveLength <= array.Length ? array : rentedDaveArray = ArrayPool<byte>.Shared.Rent(daveLength);
                    var daveSpan = daveArray.AsSpan(0, daveLength);

                    var daveResult = decryptor.Value.Decrypt(Dave.MediaType.Audio, data.Ssrc, ciphertext, daveSpan, out int bytesWritten);
                    if (daveResult is not Dave.DecryptorResultCode.Success)
                    {
                        Log(LogLevel.Debug, (Result: daveResult, Ssrc: data.Ssrc), null, static (s, e) => $"Dropping voice frame because Dave decrypt returned '{s.Result}' for SSRC {s.Ssrc}.");
                        ArrayPool<byte>.Shared.Return(array);
                        if (rentedDaveArray is not null)
                            ArrayPool<byte>.Shared.Return(rentedDaveArray);
                        return default;
                    }

                    if (rentedDaveArray is not null)
                        ArrayPool<byte>.Shared.Return(array);

                    return new VoiceReceiveEventArgs(daveArray, 0, bytesWritten, data.Ssrc);
                }
            }
        }
        catch (Exception ex)
        {
            Log<object?>(LogLevel.Error, null, ex, static (s, e) =>
            {
                return $"An error occurred while handling a datagram.{Environment.NewLine}{e}";
            });
        }

        static RtpPacket GetPacketAndSsrc(RtpPacketStorage packetStorage, out uint ssrc)
        {
            var packet = packetStorage.Packet;
            ssrc = packet.Ssrc;
            return packet;
        }
    }

    private static async ValueTask HandleTasksThatDoNotThrowAsync(ValueTask[] tasks, ushort maxIndex)
    {
        for (ushort i = 0; i <= maxIndex; i++)
            await tasks[i].ConfigureAwait(false);

        ArrayPool<ValueTask>.Shared.Return(tasks);
    }

    public ValueTask EnterSpeakingStateAsync(SpeakingProperties speaking, WebSocketPayloadProperties? properties = null, CancellationToken cancellationToken = default)
    {
        VoicePayloadProperties<SpeakingProperties> payload = new(VoiceOpcode.Speaking, speaking);
        return SendPayloadAsync(payload.Serialize(Serialization.Default.VoicePayloadPropertiesSpeakingProperties), properties, cancellationToken);
    }

    internal ValueTask SendVoiceAsync(ushort sequenceNumber, uint timestamp, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (_udpState is not { Connection: var connection, Encryption: var encryption, DaveSession: var session })
            throw new InvalidOperationException("Connection not started.");

        return SendVoiceAsync(connection, encryption, session, Cache.Ssrc, sequenceNumber, timestamp, frame, cancellationToken);
    }

    internal void SendVoice(ushort sequenceNumber, uint timestamp, ReadOnlySpan<byte> frame)
    {
        if (_udpState is not { Connection: var connection, Encryption: var encryption, DaveSession: var session })
            throw new InvalidOperationException("Connection not started.");

        frame = EncryptDave(session, Cache.Ssrc, frame, out var rentedBuffer, out var missingKeyRatchet);
        try
        {
            if (missingKeyRatchet)
                return;

            int datagramLength = frame.Length + encryption.Expansion + 12;
            var datagram = ArrayPool<byte>.Shared.Rent(datagramLength);
            try
            {
                WriteDatagram(frame, datagram.AsSpan(0, datagramLength), encryption, sequenceNumber, timestamp, Cache.Ssrc);
                connection.Send(new(datagram, 0, datagramLength));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(datagram);
            }
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private async ValueTask SendVoiceAsync(IUdpConnection connection, IVoiceEncryption encryption, DaveSession session, uint ssrc, ushort sequenceNumber, uint timestamp, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        var encryptedFrame = EncryptDave(session, ssrc, frame.Span, out var rentedBuffer, out var missingKeyRatchet);
        try
        {
            if (missingKeyRatchet)
                return;

            int datagramLength = encryptedFrame.Length + encryption.Expansion + 12;
            var datagram = ArrayPool<byte>.Shared.Rent(datagramLength);
            try
            {
                WriteDatagram(encryptedFrame, datagram.AsSpan(0, datagramLength), encryption, sequenceNumber, timestamp, ssrc);
                await connection.SendAsync(new(datagram, 0, datagramLength), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(datagram);
            }
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static ReadOnlySpan<byte> EncryptDave(DaveSession session, uint ssrc, ReadOnlySpan<byte> frame, out byte[]? rentedBuffer, out bool missingKeyRatchet)
    {
        rentedBuffer = null;
        missingKeyRatchet = false;
        if (!session.IsEnabled)
            return frame;

        var encryptor = session.Encryptor;
        int length = encryptor.GetMaxCiphertextSize(Dave.MediaType.Audio, frame.Length);
        var array = rentedBuffer = ArrayPool<byte>.Shared.Rent(length);

        while (true)
        {
            var result = encryptor.Encrypt(Dave.MediaType.Audio, ssrc, frame, array, out int bytesWritten);
            switch (result)
            {
                case Dave.EncryptorResultCode.Success:
                    return array.AsSpan(0, bytesWritten);
                case Dave.EncryptorResultCode.MissingKeyRatchet:
                    session.Client.Log(LogLevel.Debug, ssrc, null, static (s, e) => $"Dropping outgoing voice frame because Dave encryptor is missing a key ratchet for SSRC {s}.");
                    ArrayPool<byte>.Shared.Return(array);
                    rentedBuffer = null;
                    missingKeyRatchet = true;
                    return frame;
                case Dave.EncryptorResultCode.TooManyAttempts:
                    continue;
                default:
                    ArrayPool<byte>.Shared.Return(array);
                    rentedBuffer = null;
                    throw new DaveEncryptorException(result);
            }
        }
    }

    private static void WriteDatagram(ReadOnlySpan<byte> frame, Span<byte> datagram, IVoiceEncryption encryption, ushort sequenceNumber, uint timestamp, uint ssrc)
    {
        datagram[0] = 0b10000000;
        datagram[1] = 0b01111000;
        BinaryPrimitives.WriteUInt16BigEndian(datagram[2..], sequenceNumber);
        BinaryPrimitives.WriteUInt32BigEndian(datagram[4..], timestamp);
        BinaryPrimitives.WriteUInt32BigEndian(datagram[8..], ssrc);
        encryption.Encrypt(frame, new(datagram, encryption.ExtensionEncryption));
    }

    /// <summary>
    /// Creates a stream that you can write to to send voice. Each write must be exactly one Opus frame.
    /// </summary>
    /// <param name="normalizeSpeed">Whether to normalize the voice sending speed.</param>
    /// <returns></returns>
    public Stream CreateOutputStream(bool normalizeSpeed = true)
    {
        Stream stream = new VoiceOutStream(this);
        if (normalizeSpeed)
            stream = new SpeedNormalizingStream(stream);
        return stream;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cache.Dispose();
            _udpState?.Dispose();
            if (_loggerHandle.IsAllocated)
                _loggerHandle.Free();
        }
        base.Dispose(disposing);
    }
}
