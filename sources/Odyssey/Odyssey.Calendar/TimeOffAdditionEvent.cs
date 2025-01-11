namespace Odyssey.Calendar;

public interface ITimeOffAdditionEvent
{
    TimeOffType Type { get; }
    TimeOffDuration Duration { get; }
}

public sealed record PaidTimeOffAdditionEvent(TimeOffDuration Duration, TimeOffType Type) : ITimeOffAdditionEvent
{
    public static ITimeOffAdditionEvent Create(TimeAccrual timeAccrual)
    {
        var timeOffDuration = new TimeOffDuration(timeAccrual.Ticks);
        return new PaidTimeOffAdditionEvent(timeOffDuration, TimeOffType.Paid);
    }
}

public sealed record UnPaidTimeOffAdditionEvent(TimeOffDuration Duration, TimeOffType Type) : ITimeOffAdditionEvent
{
    public static ITimeOffAdditionEvent Create(TimeOffDuration duration)
    {
        return new UnPaidTimeOffAdditionEvent(duration, TimeOffType.Unpaid);
    }
}

public sealed record FamilyTimeOffAdditionEvent(TimeOffDuration Duration, TimeOffType Type) : ITimeOffAdditionEvent
{
    public static ITimeOffAdditionEvent Create(TimeOffDuration duration)
    {
        return new FamilyTimeOffAdditionEvent(duration, TimeOffType.Family);
    }
}

public sealed record TimeOffDuration(double Ticks)
{
    private TimeSpan Duration => TimeSpan.FromTicks((long)Ticks);
}

public readonly record struct TimeOffType(string TypeName)
{
    private const string PaidTypeName = "PAID";
    private const string UnpaidTypeName = "UNPAID";
    private const string FamilyTypeName = "FAMILY";
    
    public static readonly TimeOffType Paid = new TimeOffType(PaidTypeName);
    public static readonly TimeOffType Unpaid = new TimeOffType(UnpaidTypeName);
    public static readonly TimeOffType Family = new TimeOffType(FamilyTypeName);
    public static readonly TimeOffType Unknown = new TimeOffType(string.Empty);

    public static TimeOffType Parse(string value)
    {
        var target = value.ToUpperInvariant();
        
        var result = target switch
        {
            PaidTypeName => Paid,
            UnpaidTypeName => Unpaid,
            FamilyTypeName => Family,
            _ => Unknown
        };
        
        return result;
    }
    
}