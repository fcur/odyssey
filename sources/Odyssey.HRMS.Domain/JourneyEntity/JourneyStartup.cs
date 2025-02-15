namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record JourneyStartup(DateTimeOffset? StartAt, JourneyRepeatingRule? RepeatingRule)
{
    public static readonly JourneyStartup? Unset = null;

    public static JourneyStartup RepeatMonthlyAfterStart(DateTimeOffset startAt) => new JourneyStartup(startAt, JourneyRepeatingRule.Monthly);
}