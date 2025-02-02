namespace Odyssey.Domain.EmployeeEntity;

public readonly record struct AccrualPeriod(string Value)
{
    public const string YearlyTypeName = "YEARLY";
    public const string MonthlyTypeName = "MONTHLY";
    public const string WeeklyTypeName = "WEEKLY";
    public const string DailyTypeName = "DAILY";
    public const string OnceTypeName = "ONCE";

    public static readonly AccrualPeriod Yearly = new AccrualPeriod(YearlyTypeName);
    public static readonly AccrualPeriod Monthly = new AccrualPeriod(MonthlyTypeName);
    public static readonly AccrualPeriod Weekly = new AccrualPeriod(WeeklyTypeName);
    public static readonly AccrualPeriod Daily = new AccrualPeriod(DailyTypeName);
    public static readonly AccrualPeriod Once = new AccrualPeriod(OnceTypeName);
    public static readonly AccrualPeriod Unknown = new AccrualPeriod(string.Empty);

    public static bool TryParse(string value, out AccrualPeriod result)
    {
        var target = value.ToUpperInvariant();

        result = target switch
        {
            YearlyTypeName => Yearly,
            MonthlyTypeName => Monthly,
            WeeklyTypeName => Weekly,
            DailyTypeName => Daily,
            OnceTypeName => Once,
            _ => Unknown
        };

        return string.IsNullOrEmpty(result.Value);
    }
}