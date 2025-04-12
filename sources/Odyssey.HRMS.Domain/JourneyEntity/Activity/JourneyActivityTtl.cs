namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

// TODO: move TTL to Story start stage
public sealed record JourneyActivityTtl: IComparable, IComparable<JourneyActivityTtl>
{
    private readonly long _ticks;
    public JourneyActivityTtl() => _ticks = 0L;
    public JourneyActivityTtl(long ticks) => _ticks = ticks;
    public JourneyActivityTtl(TimeSpan timesSpan) => _ticks = timesSpan.Ticks;
    public static implicit operator TimeSpan(JourneyActivityTtl ttl) => TimeSpan.FromTicks(ttl._ticks);
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
        
        throw new ArgumentException($"Value is not a {nameof(JourneyActivityTtl)}");
    }
    
    public int CompareTo(JourneyActivityTtl? value)
    { 
        if (value == null)
        {
            return 1;
        }
        
        return Compare(this, value);
    }

    private static int Compare(JourneyActivityTtl v1, JourneyActivityTtl v2) => v1._ticks.CompareTo(v2._ticks);
}