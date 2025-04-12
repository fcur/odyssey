namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public sealed record JourneyActivityName(string Value)
{
    public static readonly JourneyActivityName EndOfJourney = new JourneyActivityName("EndOfJourney");
    public static readonly JourneyActivityName Unset = new JourneyActivityName(string.Empty);

    public override string ToString() => Value;
}