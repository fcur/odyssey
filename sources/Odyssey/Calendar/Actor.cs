namespace Calendar;

public sealed class Actor
{
    public User User { get; init; }
    public StartDate StartDate { get; init; }
    public TimeOffSettings TimeOffSettings { get; init; }

    public static Actor Crete(User user, StartDate startDate, TimeOffSettings timeOffSettings)
    {
        return new Actor
        {
            User = user,
            StartDate = startDate,
            TimeOffSettings = timeOffSettings
        };
    }
}

public sealed record User(UserName UserName, Email Email);

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

public sealed record PaidTimeOffDuration(TimeSpan Duration); // For PTO request

public sealed record UnPaidTimeOffDuration(TimeSpan Duration);

public sealed record FamilyTimeOffDuration(TimeSpan Duration);