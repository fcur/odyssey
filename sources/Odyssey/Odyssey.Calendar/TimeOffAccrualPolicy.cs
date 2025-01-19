namespace Odyssey.Calendar;


public readonly record struct TimeOffAccrualPolicy(string PolicyName)
{
    private const string YearlyTypeName = "YEARLY";
    private const string MonthlyTypeName = "MONTHLY";
    private const string WeeklyTypeName = "WEEKLY";
    private const string DailyTypeName = "DAILY";
    private const string OnceTypeName = "ONCE";

    public static readonly TimeOffAccrualPolicy Yearly = new TimeOffAccrualPolicy(YearlyTypeName);
    public static readonly TimeOffAccrualPolicy Monthly = new TimeOffAccrualPolicy(MonthlyTypeName);
    public static readonly TimeOffAccrualPolicy Weekly = new TimeOffAccrualPolicy(WeeklyTypeName);
    public static readonly TimeOffAccrualPolicy Daily = new TimeOffAccrualPolicy(DailyTypeName);
    public static readonly TimeOffAccrualPolicy Once = new TimeOffAccrualPolicy(OnceTypeName);
    public static readonly TimeOffAccrualPolicy Unknown = new TimeOffAccrualPolicy(string.Empty);

    public static TimeOffAccrualPolicy Parse(string value)
    {
        var target = value.ToUpperInvariant();

        var result = target switch
        {
            YearlyTypeName => Yearly,
            MonthlyTypeName => Monthly,
            WeeklyTypeName => Weekly,
            DailyTypeName => Daily,
            OnceTypeName => Once,
            _ => Unknown
        };

        return result;
    }
}