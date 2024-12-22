namespace Calendar;

public static class PredefinedPeriods
{
    public static readonly double LeapYearTicks = TimeSpan.FromDays(366).Ticks;
    public static readonly double RegularYearTicks = TimeSpan.FromDays(365).Ticks;
}

public sealed record CalendarEngine(
    Actor Actor,
    IReadOnlyCollection<ITimeOffAdditionEvent> TimeOffAdditionEvents,
    IReadOnlyCollection<ITimeOffRequest> TimeOffRequests)
{
    public static CalendarEngine Create(Actor actor,
        IReadOnlyCollection<ITimeOffAdditionEvent> timeOffAdditionEvents,
        IReadOnlyCollection<ITimeOffRequest> timeOffRequests)
    {
        return new CalendarEngine(actor, timeOffAdditionEvents, timeOffRequests);
    }

    public static TimeSpan CalculateTimeOffDuration(IReadOnlyCollection<ITimeOffAdditionEvent> timeOffAdditionEvents)
    {
        var ticks = timeOffAdditionEvents.Aggregate(0D,
            (current, timeOffAdditionEvent) => current + timeOffAdditionEvent.Duration.Ticks);
        var roundedTicks = Math.Round(ticks);
        return TimeSpan.FromTicks((long)roundedTicks);
    }

    public static TimeSpan CalculatePaidTimeOffDuration(IReadOnlyCollection<ITimeOffRequest> timeOffRequests)
    {
        return timeOffRequests.Aggregate(TimeSpan.Zero,
            (current, timeOffRequest) => current.Add(timeOffRequest.TimeOffSettings.Duration));
    }

    public TimeOffResult CalculateTimeOff(DateTimeOffset atTime)
    {
        var paidTimeOffDuration =
            CalculateTimeOffDuration(TimeOffAdditionEvents.Where(v => v.Type == TimeOffType.Paid).ToArray());
        var requestedPaidTimeOffDuration =
            CalculatePaidTimeOffDuration(TimeOffRequests.Where(v => v.Type == TimeOffType.Paid).ToArray());

        var availablePaidTimeOffDuration = paidTimeOffDuration.Subtract(requestedPaidTimeOffDuration);

        var paidTimeOff = new PaidTimeOffDuration(availablePaidTimeOffDuration);
        var unPaidTimeOff = new UnPaidTimeOffDuration(TimeSpan.Zero);
        var familyTimeOff = new FamilyTimeOffDuration(TimeSpan.Zero);
        return new TimeOffResult(paidTimeOff, unPaidTimeOff, familyTimeOff);
    }
}

public sealed record PaidTimeOffRequest(
    TimeOffRequestSettings TimeOffSettings,
    DateTimeOffset CreatedAt,
    TimeOffType Type) : ITimeOffRequest
{
    public static ITimeOffRequest Create(TimeOffRequestSettings timeOffSettings, DateTimeOffset createdAt)
    {
        return new PaidTimeOffRequest(timeOffSettings, createdAt, TimeOffType.Paid);
    }

    public static ITimeOffRequest Create(TimeOffRequestSettings timeOffSettings)
    {
        var createdAt = timeOffSettings.StartAt.AddDays(-1);
        return Create(timeOffSettings, createdAt);
    }
}

public sealed record UnPaidTimeOffRequest(
    TimeOffRequestSettings TimeOffSettings,
    DateTimeOffset CreatedAt,
    TimeOffType Type) : ITimeOffRequest
{
    public static ITimeOffRequest Create(TimeOffRequestSettings timeOffSettings, DateTimeOffset createdAt)
    {
        return new UnPaidTimeOffRequest(timeOffSettings, createdAt, TimeOffType.UnPaid);
    }
}

public sealed record FamilyTimeOffRequest(
    TimeOffRequestSettings TimeOffSettings,
    DateTimeOffset CreatedAt,
    TimeOffType Type) : ITimeOffRequest
{
    public static ITimeOffRequest Create(TimeOffRequestSettings timeOffSettings, DateTimeOffset createdAt)
    {
        return new FamilyTimeOffRequest(timeOffSettings, createdAt, TimeOffType.Family);
    }
}

public interface ITimeOffRequest
{
    TimeOffRequestSettings TimeOffSettings { get; }
    DateTimeOffset CreatedAt { get; }
    TimeOffType Type { get; }
}

public readonly record struct TimeOffType(string TypeName)
{
    public static TimeOffType Paid => new TimeOffType(nameof(Paid));
    public static TimeOffType UnPaid => new TimeOffType(nameof(UnPaid));
    public static TimeOffType Family => new TimeOffType(nameof(Family));
}

public record struct TimeOffRequestSettings(DateTimeOffset StartAt, TimeSpan Duration);

public record struct TimeOffResult(
    PaidTimeOffDuration PaidTimeOff,
    UnPaidTimeOffDuration UnPaidTimeOff,
    FamilyTimeOffDuration FamilyTimeOff);

public interface ITimeOffAdditionEvent
{
    TimeOffType Type { get; }
    TimeOffDuration Duration { get; }
}

public sealed record TimeOffDuration(double Ticks)
{
    private TimeSpan Duration => TimeSpan.FromTicks((long)Ticks);
}

public sealed record PaidTimeOffAdditionEvent(TimeOffDuration Duration, TimeOffType Type) : ITimeOffAdditionEvent
{
    // TBD: period start|end 
    public static ITimeOffAdditionEvent Create(TimeOffDuration duration)
    {
        return new PaidTimeOffAdditionEvent(duration, TimeOffType.Paid);
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