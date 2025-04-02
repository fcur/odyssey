namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

public readonly record struct JourneyActivityTtl: IComparable, IComparable<JourneyActivityTtl>
{
    private readonly long _ticks;

    public JourneyActivityTtl() => _ticks = 0L;
    public JourneyActivityTtl(long ticks) => _ticks = ticks;
    public JourneyActivityTtl(TimeSpan timesSpan) => _ticks = timesSpan.Ticks;
    public TimeSpan ToTimeSpan() => TimeSpan.FromTicks(_ticks);
    
    public static implicit operator TimeSpan(JourneyActivityTtl ttl) => ttl.ToTimeSpan();
    public static implicit operator long(JourneyActivityTtl ttl) => ttl._ticks;

    public static readonly JourneyActivityTtl? Unset = null;
    
    public int CompareTo(object? value)
    {
        if (value == null)
        {
            return 1;
        }
    
        if (value is JourneyActivityTtl ttl)
        {
            return CompareTo(ttl);
        }
        
        throw new ArgumentException("Value is not a JourneyActivityTtl");
    }
    
    public int CompareTo(JourneyActivityTtl other)
    {
        return _ticks.CompareTo(other);
    }
}