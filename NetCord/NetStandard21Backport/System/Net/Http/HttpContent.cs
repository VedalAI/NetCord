using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace System.Net.Http;

public static class HttpContentExtensions
{
    extension(HttpContent self)
    {
        public async Task<Stream> ReadAsStreamAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using (cancellationToken.Register(c => ((HttpContent)c!).Dispose(), self, useSynchronizationContext: false))
            {
                try
                {
                    return await self.ReadAsStreamAsync().ConfigureAwait(false);
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
            (ex is ObjectDisposedException);
    }
}
