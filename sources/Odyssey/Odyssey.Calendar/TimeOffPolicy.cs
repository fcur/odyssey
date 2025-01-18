namespace Odyssey.Calendar;


public readonly record struct TimeOffPolicy(string PolicyName)
{
    private const string YearlyTypeName = "YEARLY";
    private const string MonthlyTypeName = "MONTHLY";
    private const string WeeklyTypeName = "WEEKLY";
    private const string DailyTypeName = "DAILY";
    private const string OnceTypeName = "ONCE";

    public static readonly TimeOffPolicy Yearly = new TimeOffPolicy(YearlyTypeName);
    public static readonly TimeOffPolicy Monthly = new TimeOffPolicy(MonthlyTypeName);
    public static readonly TimeOffPolicy Weekly = new TimeOffPolicy(WeeklyTypeName);
    public static readonly TimeOffPolicy Daily = new TimeOffPolicy(DailyTypeName);
    public static readonly TimeOffPolicy Once = new TimeOffPolicy(OnceTypeName);
    public static readonly TimeOffPolicy Unknown = new TimeOffPolicy(string.Empty);

    public static TimeOffPolicy Parse(string value)
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