using CSharpFunctionalExtensions;

namespace Odyssey.Calendar;

public sealed class CalendarEngine
{
    public CalendarActor Actor { get; }
    public IReadOnlyCollection<TimeOffRequest> TimeOffRequests { get; }

    public CalendarEngine(CalendarActor actor, IReadOnlyCollection<TimeOffRequest> timeOffRequests)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(timeOffRequests);

        Actor = actor;
        TimeOffRequests = timeOffRequests;
    }

    public static Result<CalendarEngine> Create(CalendarActor calendarActor, IReadOnlyCollection<TimeOffRequest> timeOffRequests)
    {
        return new CalendarEngine(calendarActor, timeOffRequests);
    }

    public static TimeSpan CalculateTimeOffDuration(IReadOnlyCollection<ITimeOffAdditionEvent> timeOffAdditionEvents)
    {
        var ticks = timeOffAdditionEvents.Aggregate(0D,
            (current, timeOffAdditionEvent) => current + timeOffAdditionEvent.Duration.Ticks);
        var roundedTicks = Math.Round(ticks);
        return TimeSpan.FromTicks((long)roundedTicks);
    }

    public static TimeSpan CalculatePaidTimeOffDuration(IReadOnlyCollection<TimeOffRequest> timeOffRequests)
    {
        return timeOffRequests.Aggregate(TimeSpan.Zero,
            (current, timeOffRequest) => current.Add(timeOffRequest.TimeOffSettings.Duration));
    }

    public Result<TimeOffResult, Error> CalculateTimeOff(DateTimeOffset atTime)
    {
        var timeOffAdditionEventsResult = Actor.GetTimeOffAdditionEvents(atTime);

        if (timeOffAdditionEventsResult.IsFailure)
        {
            return timeOffAdditionEventsResult.Error;
        }
        
        var timeOffAdditionEvents = timeOffAdditionEventsResult.Value;
        
        var paidTimeOffDuration =
            CalculateTimeOffDuration(timeOffAdditionEvents.Where(v => v.Type == TimeOffType.Paid).ToArray());
        var requestedPaidTimeOffDuration =
            CalculatePaidTimeOffDuration(TimeOffRequests.Where(v => v.Type == TimeOffType.Paid).ToArray());

        var availablePaidTimeOffDuration = paidTimeOffDuration.Subtract(requestedPaidTimeOffDuration);

        var paidTimeOff = new PaidTimeOffDuration(availablePaidTimeOffDuration);
        var unPaidTimeOff = new UnpaidTimeOffDuration(TimeSpan.Zero);
        var familyTimeOff = new FamilyTimeOffDuration(TimeSpan.Zero);
        
        return new TimeOffResult(paidTimeOff, unPaidTimeOff, familyTimeOff);
    }
}

public record struct TimeOffResult(
    PaidTimeOffDuration PaidTimeOff,
    UnpaidTimeOffDuration UnpaidTimeOff,
    FamilyTimeOffDuration FamilyTimeOff);