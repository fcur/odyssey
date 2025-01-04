namespace Calendar;

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