namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public sealed record JourneyActivityEventName(string Value)
{
    public static JourneyActivityEventName ActivityStarted = new JourneyActivityEventName(nameof(ActivityStarted));
    public static JourneyActivityEventName ActivityDeclined = new JourneyActivityEventName(nameof(ActivityDeclined));
    public static JourneyActivityEventName Unset = new JourneyActivityEventName(string.Empty);
    public override string ToString() => Value;
}