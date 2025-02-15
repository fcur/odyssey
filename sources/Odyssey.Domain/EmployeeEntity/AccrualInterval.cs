namespace Odyssey.Domain.EmployeeEntity;

public readonly record struct AccrualInterval(string Value)
{
    public const string YearlyTypeName = "YEARLY";
    public const string MonthlyTypeName = "MONTHLY";
    public const string WeeklyTypeName = "WEEKLY";
    public const string DailyTypeName = "DAILY";

    public static readonly AccrualInterval Yearly = new AccrualInterval(YearlyTypeName);
    public static readonly AccrualInterval Monthly = new AccrualInterval(MonthlyTypeName);
    public static readonly AccrualInterval Weekly = new AccrualInterval(WeeklyTypeName);
    public static readonly AccrualInterval Daily = new AccrualInterval(DailyTypeName);
    public static readonly AccrualInterval Unknown = new AccrualInterval(string.Empty);

    public static bool TryParse(string value, out AccrualInterval result)
    {
        var target = value.ToUpperInvariant();

        result = target switch
        {
            YearlyTypeName => Yearly,
            MonthlyTypeName => Monthly,
            WeeklyTypeName => Weekly,
            DailyTypeName => Daily,
            _ => Unknown
        };

        return string.IsNullOrEmpty(result.Value);
    }
}