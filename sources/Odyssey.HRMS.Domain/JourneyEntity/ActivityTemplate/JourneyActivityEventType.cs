namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public record JourneyActivityEventType(byte Value, string Name)
{
    public static readonly JourneyActivityEventType Unset = new UnsetJourneyActivityEventType(); 
    public static readonly JourneyActivityEventType Source = new SourceJourneyActivityEventType();
    public static readonly JourneyActivityEventType Action = new ActionJourneyActivityEventType();
    public static readonly JourneyActivityEventType Flow = new FlowJourneyActivityEventType();
    public static readonly JourneyActivityEventType Completion = new CompletionJourneyActivityEventType();
    public static readonly JourneyActivityEventType Exit = new ExitJourneyActivityEventType();
    public override string ToString() => Name;
}

public sealed record UnsetJourneyActivityEventType() : JourneyActivityEventType(0, "Unset");
public sealed record SourceJourneyActivityEventType() : JourneyActivityEventType(1, "Source");
public sealed record ActionJourneyActivityEventType() : JourneyActivityEventType(2, "Action");
public sealed record FlowJourneyActivityEventType() : JourneyActivityEventType(3, "Flow");
public sealed record CompletionJourneyActivityEventType() : JourneyActivityEventType(4, "Completion");
public sealed record ExitJourneyActivityEventType() : JourneyActivityEventType(5, "Exit");