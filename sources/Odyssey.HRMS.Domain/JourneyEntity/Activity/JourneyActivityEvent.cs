using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

public sealed record JourneyActivityEvent(
    JourneyActivityEventName EventName,
    JourneyActivityEventType EventType,
    JourneyActivityId? NextActivityId);