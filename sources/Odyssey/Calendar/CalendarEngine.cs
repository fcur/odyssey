namespace Calendar;

public sealed record CalendarEngine(
    CalendarActor Actor,
    IReadOnlyCollection<ITimeOffRequest> TimeOffRequests)
{
    public static CalendarEngine Create(CalendarActor calendarActor,
        IReadOnlyCollection<ITimeOffRequest> timeOffRequests)
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

    public static TimeSpan CalculatePaidTimeOffDuration(IReadOnlyCollection<ITimeOffRequest> timeOffRequests)
    {
        return timeOffRequests.Aggregate(TimeSpan.Zero,
            (current, timeOffRequest) => current.Add(timeOffRequest.TimeOffSettings.Duration));
    }

    public TimeOffResult CalculateTimeOff(DateTimeOffset atTime)
    {
        var timeOffAdditionEvents = Actor.GetTimeOffAdditionEvents(atTime);
        
        var paidTimeOffDuration =
            CalculateTimeOffDuration(timeOffAdditionEvents.Where(v => v.Type == TimeOffType.Paid).ToArray());
        var requestedPaidTimeOffDuration =
            CalculatePaidTimeOffDuration(TimeOffRequests.Where(v => v.Type == TimeOffType.Paid).ToArray());

        var availablePaidTimeOffDuration = paidTimeOffDuration.Subtract(requestedPaidTimeOffDuration);

        var paidTimeOff = new PaidTimeOffDuration(availablePaidTimeOffDuration);
        var unPaidTimeOff = new UnPaidTimeOffDuration(TimeSpan.Zero);
        var familyTimeOff = new FamilyTimeOffDuration(TimeSpan.Zero);
        return new TimeOffResult(paidTimeOff, unPaidTimeOff, familyTimeOff);
    }
}

public record struct TimeOffResult(PaidTimeOffDuration PaidTimeOff, UnPaidTimeOffDuration UnPaidTimeOff, FamilyTimeOffDuration FamilyTimeOff);