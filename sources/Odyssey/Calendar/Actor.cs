namespace Calendar;

public sealed class Actor(User user, StartDate startDate, TimeOffSettings timeOffSettings)
{
    public User User { get; init; } = user ?? throw new ArgumentNullException(nameof(user));
    public StartDate StartDate { get; init; } = startDate ?? throw new ArgumentNullException(nameof(startDate));
    public TimeOffSettings TimeOffSettings { get; init; } =
        timeOffSettings ?? throw new ArgumentNullException(nameof(timeOffSettings));

    public static Actor Crete(User user, StartDate startDate, TimeOffSettings timeOffSettings)
    {
        return new Actor(user, startDate, timeOffSettings);
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
        var checkpoints = CalendarTools.BuildMonthlyCheckpoints(StartDate.Date, atTime);
        var timeAccruals = CalendarTools.PrepareMonthlyTimeAccruals(checkpoints, TimeOffSettings.PaidTimeOffDuration.Duration)
            .ToArray();

        return timeAccruals.Select(timeOffTicks => PaidTimeOffAdditionEvent.Create(new TimeOffDuration(timeOffTicks)))
            .ToArray();
    }
}

public sealed record User(UserName UserName, Email Email, Guid Id)
{
    public static User Create(UserName name, Email email)
    {
        var timeNow = DateTimeOffset.UtcNow;
        var userId = Guid.CreateVersion7(timeNow);

        return new User(name, email, userId);
    }
}

public sealed record UserName(params string[] Name);

public sealed record Email(string Value);

public sealed record StartDate(DateTimeOffset Date);

public sealed record TimeOffSettings(
    PaidTimeOffDuration PaidTimeOffDuration,
    UnPaidTimeOffDuration UnPaidTimeOffDuration,
    FamilyTimeOffDuration FamilyTimeOffDuration,
    TimeOffRounding TimeOffRounding)
{
    public static TimeOffSettings CreateDefault()
    {
        var paidTimeOffDuration = new PaidTimeOffDuration(TimeSpan.FromDays(20));
        var unPaidTimeOffDuration = new UnPaidTimeOffDuration(TimeSpan.Zero);
        var familyTimeOffDuration = new FamilyTimeOffDuration(TimeSpan.Zero);
        var timeOffRounding = new TimeOffRounding(TimeSpan.FromSeconds(1));

        return Create(paidTimeOffDuration, unPaidTimeOffDuration, familyTimeOffDuration, timeOffRounding);
    }

    public static TimeOffSettings Create(
        PaidTimeOffDuration paidTimeOffDuration,
        UnPaidTimeOffDuration unPaidTimeOffDuration,
        FamilyTimeOffDuration familyTimeOffDuration,
        TimeOffRounding timeOffRounding)
    {
        return new TimeOffSettings(paidTimeOffDuration, unPaidTimeOffDuration, familyTimeOffDuration, timeOffRounding);
    }

    public static TimeOffSettings Create(
        TimeSpan paidTimeOffDuration,
        TimeSpan timeOffRounding)
    {
        return Create(
            new PaidTimeOffDuration(paidTimeOffDuration),
            new UnPaidTimeOffDuration(TimeSpan.Zero),
            new FamilyTimeOffDuration(TimeSpan.Zero),
            new TimeOffRounding(timeOffRounding));
    }
}

public sealed record PaidTimeOffDuration(TimeSpan Duration);

public sealed record UnPaidTimeOffDuration(TimeSpan Duration);

public sealed record FamilyTimeOffDuration(TimeSpan Duration);

public sealed record TimeOffRounding(TimeSpan Interval);