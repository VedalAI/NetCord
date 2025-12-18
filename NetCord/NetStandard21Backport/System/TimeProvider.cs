using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace System;

public abstract class TimeProvider
{
    public static TimeProvider System { get; } = new SystemTimeProvider();

    protected TimeProvider()
    {
    }

    public virtual TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    public virtual long TimestampFrequency => Stopwatch.Frequency;

    public virtual DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

    public DateTimeOffset GetLocalNow() => GetUtcNow().ToOffset(LocalTimeZone.GetUtcOffset(GetUtcNow()));

    public virtual long GetTimestamp() => Stopwatch.GetTimestamp();

    public TimeSpan GetElapsedTime(long startingTimestamp) =>
        GetElapsedTime(startingTimestamp, GetTimestamp());

    public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
    {
        var frequency = TimestampFrequency;
        if (frequency == 0)
            throw new InvalidOperationException("TimestampFrequency cannot be zero.");
        
        return new TimeSpan((long)((endingTimestamp - startingTimestamp) * ((double)TimeSpan.TicksPerSecond / frequency)));
    }

    public virtual ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));

        return new SystemTimer(callback, state, dueTime, period);
    }

    private sealed class SystemTimeProvider : TimeProvider
    {
    }
}

public interface ITimer : IDisposable, IAsyncDisposable
{
    bool Change(TimeSpan dueTime, TimeSpan period);
}

internal sealed class SystemTimer : ITimer
{
    private readonly Timer _timer;
    private bool _disposed;

    public SystemTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _timer = new Timer(callback, state, dueTime, period);
    }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        if (_disposed)
            return false;

        return _timer.Change(dueTime, period);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
