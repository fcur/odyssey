using CSharpFunctionalExtensions;

namespace Odyssey.Calendar;

public record TimeOffRequest(TimeOffRequestSettings TimeOffSettings, DateTimeOffset CreatedAt, TimeOffType Type)
{
    public DateTimeOffset StartAt => TimeOffSettings.StartAt;
    public DateTimeOffset FinishAt => TimeOffSettings.FinishAt;
    
    public static TimeOffRequest CreatePaidRequest(TimeOffRequestSettings timeOffSettings,
        DateTimeOffset? createdAt = null)
    {
        return Create(timeOffSettings, TimeOffType.Paid, createdAt);
    }

    public static TimeOffRequest CreateUnpaidRequest(TimeOffRequestSettings timeOffSettings,
        DateTimeOffset? createdAt = null)
    {
        return Create(timeOffSettings, TimeOffType.Unpaid, createdAt);
    }

    public static TimeOffRequest CreateFamilyRequest(TimeOffRequestSettings timeOffSettings,
        DateTimeOffset? createdAt = null)
    {
        return Create(timeOffSettings, TimeOffType.Family, createdAt);
    }

    private static TimeOffRequest Create(TimeOffRequestSettings timeOffSettings,
        TimeOffType type,
        DateTimeOffset? createdAt = null)
    {
        return new TimeOffRequest(timeOffSettings, createdAt ?? timeOffSettings.StartAt.AddDays(-1), type);
    }
}

public record struct TimeOffRequestSettings(DateTimeOffset StartAt, TimeSpan Duration)
{
    public DateTimeOffset FinishAt => StartAt + Duration;
    
    public static Result<TimeOffRequestSettings> Create(DateTimeOffset startAt, TimeSpan duration)
    {
        return new TimeOffRequestSettings(startAt, duration);
    }

    public static Result<TimeOffRequestSettings> Create(DateTimeOffset startAt, DateTimeOffset finishAt)
    {
        if (finishAt < startAt)
        {
            return Result.Failure<TimeOffRequestSettings>(
                "Can't create time-off settings with for specified start and finish date.");
        }

        var duration = finishAt - startAt;
        return new TimeOffRequestSettings(startAt, duration);
    }
}