namespace Odyssey.Domain.EmployeeEntity;

public sealed record LeaveLimits(string Name, TimeOffLimitsType Type)
{
    public static LeaveLimits? None = null;
}

public readonly record struct TimeOffLimitsType(string PolicyName)
{
    private const string MaxValueTypeName = "MAX_VALUE";
    private const string ResetDateTypeName = "RESET_DATE";
    private const string ResetPreviousPeriodName = "RESET_PREV_PERIOD";

    public static readonly TimeOffLimitsType MaxValue = new TimeOffLimitsType(MaxValueTypeName);
    public static readonly TimeOffLimitsType ResetAtDate = new TimeOffLimitsType(ResetDateTypeName);
    public static readonly TimeOffLimitsType ResetPreviousPeriod = new TimeOffLimitsType(ResetPreviousPeriodName);
    public static readonly TimeOffLimitsType Unknown = new TimeOffLimitsType(string.Empty);

    public static TimeOffLimitsType Parse(string value)
    {
        var target = value.ToUpperInvariant();
        var result = target switch
        {
            MaxValueTypeName => MaxValue,
            ResetDateTypeName => ResetAtDate,
            ResetPreviousPeriodName => ResetPreviousPeriod,
            _ => Unknown
        };

        return result;
    }
}
