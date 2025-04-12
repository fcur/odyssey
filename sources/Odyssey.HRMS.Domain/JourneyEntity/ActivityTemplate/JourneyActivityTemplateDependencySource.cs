namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public sealed record JourneyActivityTemplateDependencySource(JourneyActivityName ActivityName, JourneyActivityEventName EventName)
{
    public static readonly JourneyActivityTemplateDependencySource? Unset = null;
}