// ReSharper disable once CheckNamespace
namespace System.Threading;

public sealed class PeriodicTimer : IDisposable
{
    private readonly TimeSpan _period;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public PeriodicTimer(TimeSpan period)
    {
        if (period <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(period));
        
        _period = period;
    }

    public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return false;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        try
        {
            await Task.Delay(_period, linkedCts.Token).ConfigureAwait(false);
            return !_disposed;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            
            return false; // Disposed
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
