namespace Odyssey.Calendar;

public sealed record TimeOffSettings(
    PaidTimeOffDuration Paid,
    UnpaidTimeOffDuration Unpaid,
    FamilyTimeOffDuration Family,
    TimeOffRounding Rounding)
{
    public static TimeOffSettings CreateDefault()
    {
        var paidTimeOffDuration = new PaidTimeOffDuration(TimeSpan.FromDays(20));
        var unPaidTimeOffDuration = new UnpaidTimeOffDuration(TimeSpan.Zero);
        var familyTimeOffDuration = new FamilyTimeOffDuration(TimeSpan.Zero);
        var timeOffRounding = new TimeOffRounding(TimeSpan.FromSeconds(1));

        return Create(paidTimeOffDuration, unPaidTimeOffDuration, familyTimeOffDuration, timeOffRounding);
    }

    public static TimeOffSettings Create(
        PaidTimeOffDuration paidTimeOffDuration,
        UnpaidTimeOffDuration unpaidTimeOffDuration,
        FamilyTimeOffDuration familyTimeOffDuration,
        TimeOffRounding timeOffRounding)
    {
        return new TimeOffSettings(paidTimeOffDuration, unpaidTimeOffDuration, familyTimeOffDuration, timeOffRounding);
    }

    public static TimeOffSettings Create(
        TimeSpan paidTimeOffDuration,
        TimeSpan timeOffRounding)
    {
        return Create(
            new PaidTimeOffDuration(paidTimeOffDuration),
            new UnpaidTimeOffDuration(TimeSpan.Zero),
            new FamilyTimeOffDuration(TimeSpan.Zero),
            new TimeOffRounding(timeOffRounding));
    }
}

public sealed record PaidTimeOffDuration(TimeSpan Value)
{
    public static PaidTimeOffDuration Parse(string duration)
    {
        return new PaidTimeOffDuration(TimeSpan.Parse(duration));
    }
}

public sealed record UnpaidTimeOffDuration(TimeSpan Value)
{
    public static UnpaidTimeOffDuration Parse(string duration)
    {
        return new UnpaidTimeOffDuration(TimeSpan.Parse(duration));
    }
}

public sealed record FamilyTimeOffDuration(TimeSpan Value)
{
    public static FamilyTimeOffDuration Parse(string duration)
    {
        return new FamilyTimeOffDuration(TimeSpan.Parse(duration));
    }
}

public sealed record TimeOffRounding(TimeSpan Value)
{
    public static TimeOffRounding Parse(string duration)
    {
        return new TimeOffRounding(TimeSpan.Parse(duration));
    }
}