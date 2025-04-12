namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record JourneyRepeatingRule(string Cron)
{
    public static readonly JourneyRepeatingRule? Unset = null;
    public static readonly JourneyRepeatingRule Monthly = new JourneyRepeatingRule("0 0 1 * *");
    public static readonly JourneyRepeatingRule Yearly = new JourneyRepeatingRule("0 0 1 1 *");
    public static readonly JourneyRepeatingRule EachMonday = new JourneyRepeatingRule("0 0 * * 1");
    public static readonly JourneyRepeatingRule EachTuesday = new JourneyRepeatingRule("0 0 * * 2");
    public static readonly JourneyRepeatingRule EachWednesday = new JourneyRepeatingRule("0 0 * * 3");
    public static readonly JourneyRepeatingRule EachThursday = new JourneyRepeatingRule("0 0 * * 4");
    public static readonly JourneyRepeatingRule EachFriday = new JourneyRepeatingRule("0 0 * * 5");
    public static readonly JourneyRepeatingRule EachSaturday = new JourneyRepeatingRule("0 0 * * 6");
    public static readonly JourneyRepeatingRule EachSunday = new JourneyRepeatingRule("0 0 * * 0");
    public static readonly JourneyRepeatingRule EachJanuary1 = new JourneyRepeatingRule("0 0 1 1 *");
}