namespace Odyssey.Calendar;

public sealed class CalendarActor(User user, StartDate startDate, TimeOffSettings timeOffSettings)
{
    public User User { get; init; } = user ?? throw new ArgumentNullException(nameof(user));
    public StartDate StartDate { get; init; } = startDate ?? throw new ArgumentNullException(nameof(startDate));

    public TimeOffSettings TimeOffSettings { get; init; } =
        timeOffSettings ?? throw new ArgumentNullException(nameof(timeOffSettings));

    public static CalendarActor Crete(User user, StartDate startDate, TimeOffSettings timeOffSettings)
    {
        return new CalendarActor(user, startDate, timeOffSettings);
    }

    public IReadOnlyCollection<ITimeOffAdditionEvent> GetTimeOffAdditionEvents(DateTimeOffset atTime)
    {
        var paidTimeOffAdditionEvents = PreparePaidTimeOffAdditionEvents(atTime);

        var unpaidTimeOffAdditionEvents = Array.Empty<ITimeOffAdditionEvent>();
        var familyTimeOffAdditionEvents = Array.Empty<ITimeOffAdditionEvent>();

        return paidTimeOffAdditionEvents;
    }

    private IReadOnlyCollection<ITimeOffAdditionEvent> PreparePaidTimeOffAdditionEvents(DateTimeOffset atTime)
    {
        var monthlyCheckpoints = CalendarTools.BuildMonthlyCheckpoints(StartDate.Value, atTime);
        var timeAccruals = CalendarTools
            .PrepareMonthlyTimeAccruals(monthlyCheckpoints, TimeOffSettings.Paid.Value)
            .ToArray();

        return timeAccruals.Select(PaidTimeOffAdditionEvent.Create).ToArray();
    }
}

public sealed record StartDate(DateTimeOffset Value)
{
    public static StartDate Parse(string data)
    {
        var dateTime = DateTimeOffset.Parse(data);
        return new StartDate(dateTime);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}