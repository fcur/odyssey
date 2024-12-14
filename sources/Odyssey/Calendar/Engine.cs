namespace Calendar;

public sealed record Engine(
    Actor Actor,
    IReadOnlyCollection<ITimeOffAdditionEvent> TimeOffAdditionEvents,
    IReadOnlyCollection<ITimeOffRequest> TimeOffRequests)
{
    public static Engine Create(Actor actor,
        IReadOnlyCollection<ITimeOffAdditionEvent> timeOffAdditionEvents,
        IReadOnlyCollection<ITimeOffRequest> timeOffRequests)
    {
        return new Engine(actor, timeOffAdditionEvents, timeOffRequests);
    }

    public TimeOffResult CalculateTimeOff(DateTimeOffset atTime)
    {
        var paidTimeOffDuration = TimeOffAdditionEvents.Aggregate(TimeSpan.Zero,
            (current, timeOffAdditionEvent) => current.Add(timeOffAdditionEvent.Duration));

        var requestedPaidTimeOffDuration = TimeOffRequests.Where(v => v.Type == TimeOffType.Paid)
            .ToArray().Aggregate(TimeSpan.Zero,
                (current, timeOffRequest) => current.Add(timeOffRequest.TimeOffSettings.Duration));

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
    TimeSpan Duration { get; }
    TimeOffType Type { get; }
}

public sealed record PaidTimeOffAdditionEvent(TimeSpan Duration, TimeOffType Type) : ITimeOffAdditionEvent
{
    public static ITimeOffAdditionEvent Create(TimeSpan duration)
    {
        return new PaidTimeOffAdditionEvent(duration, TimeOffType.Paid);
    }
}

public sealed record UnPaidTimeOffAdditionEvent(TimeSpan Duration, TimeOffType Type) : ITimeOffAdditionEvent
{
    public static ITimeOffAdditionEvent Create(TimeSpan duration)
    {
        return new UnPaidTimeOffAdditionEvent(duration, TimeOffType.UnPaid);
    }
}

public sealed record FamilyTimeOffAdditionEvent(TimeSpan Duration, TimeOffType Type) : ITimeOffAdditionEvent
{
    public static ITimeOffAdditionEvent Create(TimeSpan duration)
    {
        return new FamilyTimeOffAdditionEvent(duration, TimeOffType.Family);
    }
}