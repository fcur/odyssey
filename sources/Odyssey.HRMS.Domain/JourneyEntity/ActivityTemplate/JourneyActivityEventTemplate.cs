namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public sealed record JourneyActivityEventTemplate(
    JourneyActivityEventName EventName,
    JourneyActivityEventType EventType,
    IReadOnlyCollection<string> ResultKeys)
{
    public static JourneyActivityEventTemplate CreateSource(JourneyActivityEventName eventName, params string[] resultKeys)
    {
        return new JourneyActivityEventTemplate(eventName, JourneyActivityEventType.Source, resultKeys);
    }
    
    public static JourneyActivityEventTemplate CreateAction(JourneyActivityEventName eventName, params string[] resultKeys)
    {
        return new JourneyActivityEventTemplate(eventName, JourneyActivityEventType.Action, resultKeys);
    }
    
    public static JourneyActivityEventTemplate CreateExit(JourneyActivityEventName eventName, params string[] resultKeys)
    {
        return new JourneyActivityEventTemplate(eventName, JourneyActivityEventType.Completion, resultKeys);
    }
}