namespace Calendar;

public interface ITimeOffRequest
{
    TimeOffRequestSettings TimeOffSettings { get; }
    DateTimeOffset CreatedAt { get; }
    TimeOffType Type { get; }
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

public record struct TimeOffRequestSettings(DateTimeOffset StartAt, TimeSpan Duration);
