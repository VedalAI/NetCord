using System.Runtime.InteropServices;

namespace NetCord.Gateway.Voice;

internal static unsafe class Dave
{
    private const string DllName = "libdave";

    public const int InitTransitionId = 0;
    public const int DisabledVersion = 0;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MlsFailureCallback(IntPtr source, IntPtr reason, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogSinkCallback(LoggingSeverity severity, IntPtr file, int line, IntPtr message);

    public sealed class SessionHandle : SafeHandle
    {
        public SessionHandle() : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            SessionDestroy(handle);
            return true;
        }
    }

    public sealed class CommitResultHandle : SafeHandle
    {
        public CommitResultHandle() : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            CommitResultDestroy(handle);
            return true;
        }
    }

    public sealed class WelcomeResultHandle : SafeHandle
    {
        public WelcomeResultHandle() : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            WelcomeResultDestroy(handle);
            return true;
        }
    }

    public sealed class KeyRatchetHandle : SafeHandle
    {
        public KeyRatchetHandle() : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            KeyRatchetDestroy(handle);
            return true;
        }
    }

    public sealed class EncryptorHandle : SafeHandle
    {
        public EncryptorHandle() : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            EncryptorDestroy(handle);
            return true;
        }
    }

    public sealed class DecryptorHandle : SafeHandle
    {
        public DecryptorHandle() : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            DecryptorDestroy(handle);
            return true;
        }
    }

    public sealed class BufferHandle<T> : SafeHandle where T : unmanaged
    {
        public BufferHandle() : base(IntPtr.Zero, true)
        {
        }

        internal BufferHandle(IntPtr handle) : this()
        {
            SetHandle(handle);
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        public Span<T> AsSpan(int length) => new((void*)handle, length);

        protected override bool ReleaseHandle()
        {
            Free(handle);
            return true;
        }
    }

    public enum MediaType
    {
        Audio = 0,
        Video = 1,
    }

    public enum EncryptorResultCode
    {
        Success = 0,
        EncryptionFailure = 1,
        MissingKeyRatchet = 2,
        MissingCryptor = 3,
        TooManyAttempts = 4,
    }

    public enum DecryptorResultCode
    {
        Success = 0,
        DecryptionFailure = 1,
        MissingKeyRatchet = 2,
        InvalidNonce = 3,
        MissingCryptor = 4,
    }

    public enum LoggingSeverity
    {
        Verbose = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        None = 4,
    }

    [DllImport(DllName, EntryPoint = "daveMaxSupportedProtocolVersion", CallingConvention = CallingConvention.Cdecl)]
    public static extern ushort MaxSupportedProtocolVersion();

    [DllImport(DllName, EntryPoint = "daveFree", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Free(nint ptr);

    [DllImport(DllName, EntryPoint = "daveSessionCreate", CallingConvention = CallingConvention.Cdecl)]
    private static extern SessionHandle SessionCreateNative(IntPtr context, byte* authSessionId, MlsFailureCallback mlsFailureCallback, IntPtr userData);

    public static SessionHandle SessionCreate(IntPtr context, ReadOnlySpan<byte> authSessionId, MlsFailureCallback mlsFailureCallback, IntPtr userData)
    {
        if (authSessionId.IsEmpty)
            return SessionCreateNative(context, null, mlsFailureCallback, userData);

        fixed (byte* authSessionIdPtr = authSessionId)
            return SessionCreateNative(context, authSessionIdPtr, mlsFailureCallback, userData);
    }

    [DllImport(DllName, EntryPoint = "daveSessionDestroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SessionDestroy(nint session);

    [DllImport(DllName, EntryPoint = "daveSessionInit", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SessionInitNative(SessionHandle session, ushort version, ulong groupId, byte* selfUserId);

    public static void SessionInit(SessionHandle session, ushort version, ulong groupId, ReadOnlySpan<byte> selfUserId)
    {
        fixed (byte* selfUserIdPtr = selfUserId)
            SessionInitNative(session, version, groupId, selfUserIdPtr);
    }

    [DllImport(DllName, EntryPoint = "daveSessionReset", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SessionReset(SessionHandle session);

    [DllImport(DllName, EntryPoint = "daveSessionGetProtocolVersion", CallingConvention = CallingConvention.Cdecl)]
    public static extern ushort SessionGetProtocolVersion(SessionHandle session);

    [DllImport(DllName, EntryPoint = "daveSessionSetExternalSender", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SessionSetExternalSenderNative(SessionHandle session, byte* externalSender, nuint length);

    public static void SessionSetExternalSender(SessionHandle session, ReadOnlySpan<byte> externalSender, nuint length)
    {
        fixed (byte* externalSenderPtr = externalSender)
            SessionSetExternalSenderNative(session, externalSenderPtr, length);
    }

    [DllImport(DllName, EntryPoint = "daveSessionProcessProposals", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SessionProcessProposalsNative(SessionHandle session, byte* proposals, nuint length, nint* recognizedUserIds, nuint recognizedUserIdsLength, out IntPtr commitWelcomeBytes, out nuint commitWelcomeBytesLength);

    public static void SessionProcessProposals(SessionHandle session, ReadOnlySpan<byte> proposals, nuint length, ReadOnlySpan<nint> recognizedUserIds, nuint recognizedUserIdsLength, out BufferHandle<byte>? commitWelcomeBytes, out nuint commitWelcomeBytesLength)
    {
        fixed (byte* proposalsPtr = proposals)
        fixed (nint* recognizedUserIdsPtr = recognizedUserIds)
        {
            SessionProcessProposalsNative(session, proposalsPtr, length, recognizedUserIdsPtr, recognizedUserIdsLength, out var commitWelcomePtr, out commitWelcomeBytesLength);
            commitWelcomeBytes = commitWelcomePtr == IntPtr.Zero ? null : new BufferHandle<byte>(commitWelcomePtr);
        }
    }

    [DllImport(DllName, EntryPoint = "daveSessionProcessCommit", CallingConvention = CallingConvention.Cdecl)]
    private static extern CommitResultHandle SessionProcessCommitNative(SessionHandle session, byte* commit, nuint length);

    public static CommitResultHandle SessionProcessCommit(SessionHandle session, ReadOnlySpan<byte> commit, nuint length)
    {
        fixed (byte* commitPtr = commit)
            return SessionProcessCommitNative(session, commitPtr, length);
    }

    [DllImport(DllName, EntryPoint = "daveSessionProcessWelcome", CallingConvention = CallingConvention.Cdecl)]
    private static extern WelcomeResultHandle SessionProcessWelcomeNative(SessionHandle session, byte* welcome, nuint length, nint* recognizedUserIds, nuint recognizedUserIdsLength);

    public static WelcomeResultHandle SessionProcessWelcome(SessionHandle session, ReadOnlySpan<byte> welcome, nuint length, ReadOnlySpan<nint> recognizedUserIds, nuint recognizedUserIdsLength)
    {
        fixed (byte* welcomePtr = welcome)
        fixed (nint* recognizedUserIdsPtr = recognizedUserIds)
            return SessionProcessWelcomeNative(session, welcomePtr, length, recognizedUserIdsPtr, recognizedUserIdsLength);
    }

    [DllImport(DllName, EntryPoint = "daveSessionGetMarshalledKeyPackage", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SessionGetMarshalledKeyPackageNative(SessionHandle session, out IntPtr keyPackage, out nuint length);

    public static void SessionGetMarshalledKeyPackage(SessionHandle session, out BufferHandle<byte>? keyPackage, out nuint length)
    {
        SessionGetMarshalledKeyPackageNative(session, out var keyPackagePtr, out length);
        keyPackage = keyPackagePtr == IntPtr.Zero ? null : new BufferHandle<byte>(keyPackagePtr);
    }

    [DllImport(DllName, EntryPoint = "daveSessionGetKeyRatchet", CallingConvention = CallingConvention.Cdecl)]
    private static extern KeyRatchetHandle SessionGetKeyRatchetNative(SessionHandle session, byte* userId);

    public static KeyRatchetHandle SessionGetKeyRatchet(SessionHandle session, ReadOnlySpan<byte> userId)
    {
        fixed (byte* userIdPtr = userId)
            return SessionGetKeyRatchetNative(session, userIdPtr);
    }

    [DllImport(DllName, EntryPoint = "daveKeyRatchetDestroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern void KeyRatchetDestroy(nint keyRatchet);

    [DllImport(DllName, EntryPoint = "daveCommitResultIsFailed", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool CommitResultIsFailed(CommitResultHandle commitResultHandle);

    [DllImport(DllName, EntryPoint = "daveCommitResultIsIgnored", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool CommitResultIsIgnored(CommitResultHandle commitResultHandle);

    [DllImport(DllName, EntryPoint = "daveCommitResultDestroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern void CommitResultDestroy(nint commitResultHandle);

    [DllImport(DllName, EntryPoint = "daveWelcomeResultDestroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern void WelcomeResultDestroy(nint welcomeResultHandle);

    [DllImport(DllName, EntryPoint = "daveEncryptorCreate", CallingConvention = CallingConvention.Cdecl)]
    public static extern EncryptorHandle EncryptorCreate();

    [DllImport(DllName, EntryPoint = "daveEncryptorDestroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern void EncryptorDestroy(nint encryptor);

    [DllImport(DllName, EntryPoint = "daveEncryptorSetKeyRatchet", CallingConvention = CallingConvention.Cdecl)]
    public static extern void EncryptorSetKeyRatchet(EncryptorHandle encryptor, KeyRatchetHandle keyRatchet);

    [DllImport(DllName, EntryPoint = "daveEncryptorGetMaxCiphertextByteSize", CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint EncryptorGetMaxCiphertextByteSize(EncryptorHandle encryptor, MediaType mediaType, nuint frameSize);

    [DllImport(DllName, EntryPoint = "daveEncryptorEncrypt", CallingConvention = CallingConvention.Cdecl)]
    private static extern EncryptorResultCode EncryptorEncryptNative(EncryptorHandle encryptor, MediaType mediaType, uint ssrc, byte* frame, nuint frameLength, byte* encryptedFrame, nuint encryptedFrameCapacity, out nuint bytesWritten);

    public static EncryptorResultCode EncryptorEncrypt(EncryptorHandle encryptor, MediaType mediaType, uint ssrc, ReadOnlySpan<byte> frame, nuint frameLength, Span<byte> encryptedFrame, nuint encryptedFrameCapacity, out nuint bytesWritten)
    {
        fixed (byte* framePtr = frame)
        fixed (byte* encryptedFramePtr = encryptedFrame)
            return EncryptorEncryptNative(encryptor, mediaType, ssrc, framePtr, frameLength, encryptedFramePtr, encryptedFrameCapacity, out bytesWritten);
    }

    [DllImport(DllName, EntryPoint = "daveDecryptorCreate", CallingConvention = CallingConvention.Cdecl)]
    public static extern DecryptorHandle DecryptorCreate();

    [DllImport(DllName, EntryPoint = "daveDecryptorDestroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DecryptorDestroy(nint decryptor);

    [DllImport(DllName, EntryPoint = "daveDecryptorTransitionToKeyRatchet", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DecryptorTransitionToKeyRatchet(DecryptorHandle decryptor, KeyRatchetHandle keyRatchet);

    [DllImport(DllName, EntryPoint = "daveDecryptorTransitionToPassthroughMode", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DecryptorTransitionToPassthroughMode(DecryptorHandle decryptor, [MarshalAs(UnmanagedType.I1)] bool passthroughMode);

    [DllImport(DllName, EntryPoint = "daveDecryptorDecrypt", CallingConvention = CallingConvention.Cdecl)]
    private static extern DecryptorResultCode DecryptorDecryptNative(DecryptorHandle decryptor, MediaType mediaType, byte* encryptedFrame, nuint encryptedFrameLength, byte* frame, nuint frameCapacity, out nuint bytesWritten);

    public static DecryptorResultCode DecryptorDecrypt(DecryptorHandle decryptor, MediaType mediaType, ReadOnlySpan<byte> encryptedFrame, nuint encryptedFrameLength, Span<byte> frame, nuint frameCapacity, out nuint bytesWritten)
    {
        fixed (byte* encryptedFramePtr = encryptedFrame)
        fixed (byte* framePtr = frame)
            return DecryptorDecryptNative(decryptor, mediaType, encryptedFramePtr, encryptedFrameLength, framePtr, frameCapacity, out bytesWritten);
    }

    [DllImport(DllName, EntryPoint = "daveDecryptorGetMaxPlaintextByteSize", CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint DecryptorGetMaxPlaintextByteSize(DecryptorHandle decryptor, MediaType mediaType, nuint encryptedFrameSize);

    [DllImport(DllName, EntryPoint = "daveSetLogSinkCallback", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetLogSinkCallback(LogSinkCallback callback);
}
