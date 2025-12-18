using System.Globalization;

// ReSharper disable once CheckNamespace
namespace System;

public readonly struct TimeOnly : IComparable, IComparable<TimeOnly>, IEquatable<TimeOnly>, IFormattable
{
    private readonly long _ticks;

    public static TimeOnly MinValue => new(0);
    public static TimeOnly MaxValue => new(MaxTicks);

    private const long TicksPerMillisecond = 10000;
    private const long TicksPerSecond = TicksPerMillisecond * 1000;
    private const long TicksPerMinute = TicksPerSecond * 60;
    private const long TicksPerHour = TicksPerMinute * 60;
    private const long MaxTicks = TicksPerHour * 24 - 1;

    public TimeOnly(long ticks)
    {
        if ((ulong)ticks > MaxTicks)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        _ticks = ticks;
    }

    public TimeOnly(int hour, int minute)
        : this(hour, minute, 0, 0, 0) { }

    public TimeOnly(int hour, int minute, int second)
        : this(hour, minute, second, 0, 0) { }

    public TimeOnly(int hour, int minute, int second, int millisecond)
        : this(hour, minute, second, millisecond, 0) { }

    public TimeOnly(int hour, int minute, int second, int millisecond, int microsecond)
    {
        if ((uint)hour > 23) throw new ArgumentOutOfRangeException(nameof(hour));
        if ((uint)minute > 59) throw new ArgumentOutOfRangeException(nameof(minute));
        if ((uint)second > 59) throw new ArgumentOutOfRangeException(nameof(second));
        if ((uint)millisecond > 999) throw new ArgumentOutOfRangeException(nameof(millisecond));
        if ((uint)microsecond > 999) throw new ArgumentOutOfRangeException(nameof(microsecond));

        _ticks = hour * TicksPerHour +
                 minute * TicksPerMinute +
                 second * TicksPerSecond +
                 millisecond * TicksPerMillisecond +
                 microsecond * 10;
    }

    public int Hour => (int)(_ticks / TicksPerHour);
    public int Minute => (int)(_ticks / TicksPerMinute % 60);
    public int Second => (int)(_ticks / TicksPerSecond % 60);
    public int Millisecond => (int)(_ticks / TicksPerMillisecond % 1000);
    public int Microsecond => (int)(_ticks / 10 % 1000);
    public long Ticks => _ticks;

    public TimeOnly Add(TimeSpan value) => AddTicks(value.Ticks);

    public TimeOnly Add(TimeSpan value, out int wrappedDays)
    {
        long ticks = _ticks + value.Ticks;
        wrappedDays = (int)(ticks / (MaxTicks + 1));
        if (ticks < 0)
        {
            wrappedDays--;
            ticks = (MaxTicks + 1) + (ticks % (MaxTicks + 1));
            if (ticks == MaxTicks + 1)
            {
                ticks = 0;
                wrappedDays++;
            }
        }
        else
        {
            ticks %= (MaxTicks + 1);
        }
        return new TimeOnly(ticks);
    }

    public TimeOnly AddHours(double value) => AddTicks((long)(value * TicksPerHour));

    public TimeOnly AddMinutes(double value) => AddTicks((long)(value * TicksPerMinute));

    private TimeOnly AddTicks(long ticks)
    {
        ticks = (_ticks + ticks) % (MaxTicks + 1);
        if (ticks < 0)
            ticks += MaxTicks + 1;
        return new TimeOnly(ticks);
    }

    public bool IsBetween(TimeOnly start, TimeOnly end)
    {
        if (start._ticks <= end._ticks)
            return _ticks >= start._ticks && _ticks < end._ticks;
        
        return _ticks >= start._ticks || _ticks < end._ticks;
    }

    public TimeSpan ToTimeSpan() => new(_ticks);

    public static TimeOnly FromTimeSpan(TimeSpan timeSpan)
    {
        long ticks = timeSpan.Ticks % (MaxTicks + 1);
        if (ticks < 0)
            ticks += MaxTicks + 1;
        return new TimeOnly(ticks);
    }

    public static TimeOnly FromDateTime(DateTime dateTime) => new(dateTime.TimeOfDay.Ticks);

    public static TimeOnly Parse(string s) => Parse(s, null);

    public static TimeOnly Parse(string s, IFormatProvider? provider)
    {
        var dt = DateTime.Parse(s, provider, DateTimeStyles.NoCurrentDateDefault);
        return FromDateTime(dt);
    }

    public static bool TryParse(string? s, out TimeOnly result) => TryParse(s, null, out result);

    public static bool TryParse(string? s, IFormatProvider? provider, out TimeOnly result)
    {
        if (DateTime.TryParse(s, provider, DateTimeStyles.NoCurrentDateDefault, out var dt))
        {
            result = FromDateTime(dt);
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryParseExact(string? s, string? format, out TimeOnly result) =>
        TryParseExact(s, format, null, DateTimeStyles.None, out result);

    public static bool TryParseExact(string? s, string? format, IFormatProvider? provider, DateTimeStyles style, out TimeOnly result)
    {
        if (DateTime.TryParseExact(s, format, provider, style | DateTimeStyles.NoCurrentDateDefault, out var dt))
        {
            result = FromDateTime(dt);
            return true;
        }
        result = default;
        return false;
    }

    public int CompareTo(TimeOnly other) => _ticks.CompareTo(other._ticks);

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is TimeOnly other) return CompareTo(other);
        throw new ArgumentException("Object must be of type TimeOnly.");
    }

    public bool Equals(TimeOnly other) => _ticks == other._ticks;

    public override bool Equals(object? obj) => obj is TimeOnly other && Equals(other);

    public override int GetHashCode() => _ticks.GetHashCode();

    public override string ToString() => ToString("t", null);

    public string ToString(string? format) => ToString(format, null);

    public string ToString(IFormatProvider? provider) => ToString("t", provider);

    public string ToString(string? format, IFormatProvider? provider)
    {
        if (string.IsNullOrEmpty(format))
            format = "t";

        var dt = new DateTime(_ticks);
        return dt.ToString(format, provider);
    }

    public static bool operator ==(TimeOnly left, TimeOnly right) => left._ticks == right._ticks;
    public static bool operator !=(TimeOnly left, TimeOnly right) => left._ticks != right._ticks;
    public static bool operator <(TimeOnly left, TimeOnly right) => left._ticks < right._ticks;
    public static bool operator <=(TimeOnly left, TimeOnly right) => left._ticks <= right._ticks;
    public static bool operator >(TimeOnly left, TimeOnly right) => left._ticks > right._ticks;
    public static bool operator >=(TimeOnly left, TimeOnly right) => left._ticks >= right._ticks;

    public static TimeSpan operator -(TimeOnly left, TimeOnly right)
    {
        long diff = left._ticks - right._ticks;
        if (diff < -(MaxTicks + 1) / 2)
            diff += MaxTicks + 1;
        else if (diff > (MaxTicks + 1) / 2)
            diff -= MaxTicks + 1;
        return new TimeSpan(diff);
    }
}
