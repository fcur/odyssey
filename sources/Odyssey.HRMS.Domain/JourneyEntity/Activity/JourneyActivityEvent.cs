using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

public sealed record JourneyActivityEvent(JourneyActivityEventName Name, JourneyActivityEventType Type, JourneyActivityId? NextActivityId)
{
    public static readonly JourneyActivityEvent ActivityStarted = new JourneyActivityEvent(JourneyActivityEventName.ActivityStarted, JourneyActivityEventType.Flow, null);
    public static readonly JourneyActivityEvent ActivityDeclined = new JourneyActivityEvent(JourneyActivityEventName.ActivityDeclined, JourneyActivityEventType.Flow, null);
}



