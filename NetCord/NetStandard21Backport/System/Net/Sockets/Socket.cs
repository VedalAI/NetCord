using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace System.Net.Sockets;

public static class SocketExtensions
{
    extension(Socket self)
    {
        public async ValueTask ConnectAsync(EndPoint remoteEp, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using (cancellationToken.Register(s => ((Socket)s!).Dispose(), self, useSynchronizationContext: false))
            {
                try
                {
                    await self.ConnectAsync(remoteEp).ConfigureAwait(false);
                }
                catch (Exception ex) when (HandleException(ex, cancellationToken))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        public async ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using (cancellationToken.Register(s => ((Socket)s!).Dispose(), self, useSynchronizationContext: false))
            {
                try
                {
                    if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                    {
                        return await self.SendAsync(segment, SocketFlags.None).ConfigureAwait(false);
                    }
                    else
                    {
                        var array = buffer.ToArray();
                        return await self.SendAsync(new ArraySegment<byte>(array), SocketFlags.None).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (HandleException(ex, cancellationToken))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        public async ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using (cancellationToken.Register(s => ((Socket)s!).Dispose(), self, useSynchronizationContext: false))
            {
                try
                {
                    if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                    {
                        return await self.ReceiveAsync(segment, SocketFlags.None).ConfigureAwait(false);
                    }
                    else
                    {
                        var array = new byte[buffer.Length];
                        int received = await self.ReceiveAsync(new ArraySegment<byte>(array), SocketFlags.None).ConfigureAwait(false);
                        array.AsSpan(0, received).CopyTo(buffer.Span);
                        return received;
                    }
                }
                catch (Exception ex) when (HandleException(ex, cancellationToken))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HandleException(Exception ex, CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested &&
               (ex is ObjectDisposedException ||
                ex is SocketException se && se.SocketErrorCode == SocketError.OperationAborted);
    }
}
