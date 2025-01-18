namespace Odyssey.Calendar;

public sealed record TimeOffSettingsV2(IReadOnlyCollection<RecurringTimeOff> RecurringTimeOff)
{
    // TBD: Marriage leave & Maternity leave as TimeOff request
    public TimeOffSettingsV2 CreateDefault()
    {
        var timeOffRounding = new TimeOffRounding(TimeSpan.FromSeconds(1));
        var paidTimeOffAccrual = new TimeOffAccrual(TimeSpan.FromDays(20));
        var unpaidTimeOffAccrual = new TimeOffAccrual(TimeSpan.FromDays(20));
        var familyTimeOffAccrual = new TimeOffAccrual(TimeSpan.FromDays(1));

        var paidTimeOff = new RecurringTimeOff(TimeOffType.Paid, paidTimeOffAccrual, TimeOffPolicy.Yearly, timeOffRounding);
        var unpaidTimeOff = new RecurringTimeOff(TimeOffType.Unpaid, unpaidTimeOffAccrual, TimeOffPolicy.Yearly, timeOffRounding);
        var familyTimeOff = new RecurringTimeOff(TimeOffType.Family, familyTimeOffAccrual, TimeOffPolicy.Monthly, timeOffRounding);

        return new TimeOffSettingsV2([paidTimeOff, unpaidTimeOff, familyTimeOff]);
    }
}

public readonly record struct TimeOffAccrual(TimeSpan Value)
{
    public static TimeOffAccrual Parse(string duration)
    {
        return new TimeOffAccrual(TimeSpan.Parse(duration));
    }
}

public sealed record RecurringTimeOff(
    // TBD: TimeOffLimits
    TimeOffType Type,
    TimeOffAccrual Accrual,
    TimeOffPolicy Policy,
    TimeOffRounding Rounding);

public sealed record CustomTimeOff(string Name, string Description);

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