using Odyssey.Domain.EmployeeEntity;

namespace Odyssey.Domain;

public readonly record struct LeaveAccrual(TimeSpan Value, AccrualPeriod Period, AccrualInterval Interval)
{
    public static bool TryParse(string duration, string policy, string interval, out LeaveAccrual result)
    {
        var durationTs = TimeSpan.Parse(duration);
        var isValidPolicy = AccrualPeriod.TryParse(policy, out var policyResult);
        var isValidInterval = AccrualInterval.TryParse(interval, out var intervalResult);

        result = new LeaveAccrual(durationTs, policyResult, intervalResult);

        return isValidPolicy && isValidInterval;
    }
}