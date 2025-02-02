using CSharpFunctionalExtensions;
using Odyssey.Domain.EmployeeEntity;

namespace Odyssey.Domain;

public record TimeOffRequest(
    TimeOffRequestSettings TimeOffSettings,
    DateTimeOffset CreatedAt,
    TimeOffType Type,
    LeaveType LeaveType)
{
    public DateTimeOffset StartAt => TimeOffSettings.StartAt;
    public DateTimeOffset FinishAt => TimeOffSettings.FinishAt;

    public static TimeOffRequest CreatePaidTimeOffRequest(TimeOffRequestSettings timeOffSettings,
        DateTimeOffset? createdAt = null)
    {
        return Create(timeOffSettings, TimeOffType.Paid, LeaveType.PaidTimeOff, createdAt);
    }

    public static TimeOffRequest CreateUnpaidTimeOffRequest(TimeOffRequestSettings timeOffSettings,
        DateTimeOffset? createdAt = null)
    {
        return Create(timeOffSettings, TimeOffType.Unpaid, LeaveType.UnpaidTimeOff, createdAt);
    }

    private static TimeOffRequest Create(TimeOffRequestSettings timeOffSettings,
        TimeOffType type,
        LeaveType leaveType,
        DateTimeOffset? createdAt = null)
    {
        return new TimeOffRequest(timeOffSettings, createdAt ?? timeOffSettings.StartAt.AddDays(-1), type, leaveType);
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