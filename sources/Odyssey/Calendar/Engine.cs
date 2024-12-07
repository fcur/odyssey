using System.Runtime.InteropServices.JavaScript;

namespace Calendar;

public sealed class Engine(Actor actor, IReadOnlyCollection<ITimeOffRequest> TimeOffRequests)
{
    public static Engine Create(Actor actor, IReadOnlyCollection<ITimeOffRequest> timeOffRequests)
    {
        return new Engine(actor, timeOffRequests);
    }

    public TimeOffResult CalculateTimeOff(DateTimeOffset atTime)
    {
        var paidTimeOff = new PaidTimeOffDuration(TimeSpan.Zero);
        var unPaidTimeOff = new UnPaidTimeOffDuration(TimeSpan.Zero);
        var familyTimeOff = new FamilyTimeOffDuration(TimeSpan.Zero);
        return new TimeOffResult(paidTimeOff, unPaidTimeOff, familyTimeOff);
    }
}

public sealed record PaidTimeOffRequest(
    TimeOffRequestSettings TimeOffSettings,
    DateTimeOffset CreatedAt,
    TimeOffRequestType RequestType) : ITimeOffRequest
{
    public static ITimeOffRequest Create(TimeOffRequestSettings timeOffSettings, DateTimeOffset createdAt)
    {
        var requestType = new TimeOffRequestType(nameof(PaidTimeOffRequest));

        return new PaidTimeOffRequest(timeOffSettings, createdAt, requestType);
    }
}

public sealed record UnPaidTimeOffRequest(
    TimeOffRequestSettings TimeOffSettings,
    DateTimeOffset CreatedAt,
    TimeOffRequestType RequestType) : ITimeOffRequest
{
    public static ITimeOffRequest Create(TimeOffRequestSettings timeOffSettings, DateTimeOffset createdAt)
    {
        var requestType = new TimeOffRequestType(nameof(UnPaidTimeOffRequest));

        return new UnPaidTimeOffRequest(timeOffSettings, createdAt, requestType);
    }
}

public sealed record FamilyTimeOffRequest(
    TimeOffRequestSettings TimeOffSettings,
    DateTimeOffset CreatedAt,
    TimeOffRequestType RequestType) : ITimeOffRequest
{
    public static ITimeOffRequest Create(TimeOffRequestSettings timeOffSettings, DateTimeOffset createdAt)
    {
        var requestType = new TimeOffRequestType(nameof(FamilyTimeOffRequest));

        return new FamilyTimeOffRequest(timeOffSettings, createdAt, requestType);
    }
}

public interface ITimeOffRequest
{
    TimeOffRequestSettings TimeOffSettings { get; }
    DateTimeOffset CreatedAt { get; }
    TimeOffRequestType RequestType { get; }
}

public record struct TimeOffRequestType(string RequestType);

public record struct TimeOffRequestSettings(DateTimeOffset StartAt, TimeSpan Duration);

public record struct TimeOffResult(
    PaidTimeOffDuration PaidTimeOff,
    UnPaidTimeOffDuration UnPaidTimeOff,
    FamilyTimeOffDuration FamilyTimeOff);