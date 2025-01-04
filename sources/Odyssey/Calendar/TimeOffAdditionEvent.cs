namespace Calendar;

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
        return new UnPaidTimeOffAdditionEvent(duration, TimeOffType.UnPaid);
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
    public static TimeOffType Paid => new TimeOffType(nameof(Paid));
    public static TimeOffType UnPaid => new TimeOffType(nameof(UnPaid));
    public static TimeOffType Family => new TimeOffType(nameof(Family));
}