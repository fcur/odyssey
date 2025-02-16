namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public sealed record JourneyActivityEventType(byte Value, string Name)
{
    public static readonly JourneyActivityEventType Unset = new JourneyActivityEventType(0, nameof(Unset));
    public static readonly JourneyActivityEventType Source = new JourneyActivityEventType(1, nameof(Source));
    public static readonly JourneyActivityEventType Action = new JourneyActivityEventType(2, nameof(Action));
    public static readonly JourneyActivityEventType Exit = new JourneyActivityEventType(3, nameof(Exit));

    public override string ToString() => Name;
}