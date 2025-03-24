namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public abstract record JourneyActivityEventType(string Name)
{
    public static readonly JourneyActivityEventType Source = new SourceJourneyActivityEventType();
    public static readonly JourneyActivityEventType Flow = new FlowJourneyActivityEventType();
    public static readonly JourneyActivityEventType Success = new SuccessJourneyActivityEventType();
    public static readonly JourneyActivityEventType Fail = new FailJourneyActivityEventType();
    public static readonly JourneyActivityEventType Exit = new ExitJourneyActivityEventType(); 
    public override string ToString() => Name;
}

public sealed record SourceJourneyActivityEventType() : JourneyActivityEventType("Source");
public sealed record FlowJourneyActivityEventType() : JourneyActivityEventType("Flow");
public sealed record SuccessJourneyActivityEventType() : JourneyActivityEventType("Success");
public sealed record FailJourneyActivityEventType() : JourneyActivityEventType("Fail");
public sealed record ExitJourneyActivityEventType() : JourneyActivityEventType("Exit");