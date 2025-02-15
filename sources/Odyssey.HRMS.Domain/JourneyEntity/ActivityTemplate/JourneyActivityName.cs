namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public sealed record JourneyActivityName(string Value)
{
    public static readonly JourneyActivityName EndOfJourney = new JourneyActivityName("EndOfJourney");
}