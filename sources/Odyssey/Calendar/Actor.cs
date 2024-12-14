namespace Calendar;

public sealed class Actor
{
    public User User { get; init; }
    public StartDate StartDate { get; init; }
    public TimeOffSettings TimeOffSettings { get; init; }

    private static readonly TimeSpan LeapYear = TimeSpan.FromDays(366);
    private static readonly TimeSpan RegularYear = TimeSpan.FromDays(365);


    public static Actor Crete(User user, StartDate startDate, TimeOffSettings timeOffSettings)
    {
        return new Actor
        {
            User = user,
            StartDate = startDate,
            TimeOffSettings = timeOffSettings
        };
    }

    public IReadOnlyCollection<ITimeOffAdditionEvent> GetTimeOffAdditionEvents(DateTimeOffset atTime)
    {
        var timeOffAdditionEvents = new List<ITimeOffAdditionEvent>();

        var nextMonth = StartDate.Date.AddMonths(1);
        var time = new DateTimeOffset(nextMonth.Year, nextMonth.Month, 1, 0, 0, 0, StartDate.Date.Offset);

        if (time > atTime)
        {
            time = atTime;
        }

        var checkpoints = new List<DateTimeOffset> { StartDate.Date };

        while (time < atTime)
        {
            checkpoints.Add(time);
            time = time.AddMonths(1);
        }

        checkpoints.Add(atTime);
        
        for (int i = 0, j = 1; j < checkpoints.Count; i++, j++)
        {
            var periodStart = checkpoints[i];
            var periodEnd = checkpoints[j];

            var duration = periodEnd - periodStart;
            double ticksInYear = DateTime.IsLeapYear(periodStart.Year) ? LeapYear.Ticks : RegularYear.Ticks;

            var timeOffTicks = double.Round(duration.Ticks / ticksInYear, 10) *
                               TimeOffSettings.PaidTimeOffDuration.Duration.Ticks;

            var periodTimeOffDuration = new TimeSpan((long)timeOffTicks);
            timeOffAdditionEvents.Add(PaidTimeOffAdditionEvent.Create(periodTimeOffDuration));
        }

        return timeOffAdditionEvents.ToArray();
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
    FamilyTimeOffDuration FamilyTimeOffDuration)
{
    public static TimeOffSettings CreateDefault()
    {
        var paidTimeOffDuration = new PaidTimeOffDuration(TimeSpan.FromDays(20));
        var unPaidTimeOffDuration = new UnPaidTimeOffDuration(TimeSpan.Zero);
        var familyTimeOffDuration = new FamilyTimeOffDuration(TimeSpan.Zero);

        return new TimeOffSettings(paidTimeOffDuration, unPaidTimeOffDuration, familyTimeOffDuration);
    }
}

// For PTO request
public sealed record PaidTimeOffDuration(TimeSpan Duration)
{
    // public static PaidTimeOffDuration Create(TimeSpan duration)
    // {
    //     // var step = float.Round(duration.Days / 365.0F, 3);
    //     
    //     return new PaidTimeOffDuration(duration, step);
    // }        
}

public sealed record UnPaidTimeOffDuration(TimeSpan Duration);

public sealed record FamilyTimeOffDuration(TimeSpan Duration);