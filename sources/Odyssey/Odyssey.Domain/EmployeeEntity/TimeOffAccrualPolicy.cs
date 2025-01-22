namespace Odyssey.Domain.EmployeeEntity;

public readonly record struct TimeOffAccrualPolicy(string Name)
{
    public const string YearlyTypeName = "YEARLY";
    public const string MonthlyTypeName = "MONTHLY";
    public const string WeeklyTypeName = "WEEKLY";
    public const string DailyTypeName = "DAILY";
    public const string OnceTypeName = "ONCE";

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