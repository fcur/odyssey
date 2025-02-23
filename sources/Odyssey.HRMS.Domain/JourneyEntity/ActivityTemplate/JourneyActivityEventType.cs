namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public record JourneyActivityEventType(byte Value, string Name)
{
    public static readonly JourneyActivityEventType Unset = new JourneyActivityEventType(0, nameof(Unset));
    public static readonly JourneyActivityEventType Source = new JourneyActivityEventType(1, nameof(Source));
    public static readonly JourneyActivityEventType Action = new JourneyActivityEventType(2, nameof(Action));
    public static readonly JourneyActivityEventType Exit = new JourneyActivityEventType(3, nameof(Exit));
    public override string ToString() => Name;

    public static JourneyActivityEventType Parse(string name)
    {
        var target = name.ToUpperInvariant();
        return target switch
        {
            "UNSET" => new UnsetJourneyActivityEventType(),
            "SOURCE" => new SourceJourneyActivityEventType(),
            "ACTION" => new ActionJourneyActivityEventType(),
            "EXIT" => new ExitJourneyActivityEventType(),
            _ => new UnsetJourneyActivityEventType(),
        };
    }
}

public sealed record UnsetJourneyActivityEventType() : JourneyActivityEventType(0, "Unset");

public sealed record SourceJourneyActivityEventType() : JourneyActivityEventType(1, "Source");

public sealed record ActionJourneyActivityEventType() : JourneyActivityEventType(2, "Action");

public sealed record ExitJourneyActivityEventType() : JourneyActivityEventType(3, "Exit");