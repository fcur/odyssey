using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

public sealed record JourneyActivityEvent(
    JourneyActivityEventName Name,
    JourneyActivityEventType Type,
    JourneyActivityId? NextActivityId);