using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public sealed record JourneyStoryError : DomainError
{
    private JourneyStoryError(string type, string message) : base(type, message) { }

    public static JourneyStoryError Validation(string message) => new JourneyStoryError("Validation", message);
    
    public static JourneyStoryError UnsupportedActivity(Guid activityId) => new JourneyStoryError("UnsupportedActivity", $"Unsupported activity '{activityId:D}'");
    public static JourneyStoryError UnsupportedActivityAction(string activityName, string action) => new JourneyStoryError("UnsupportedActivityAction", $"Can't handle action '{action}' for activity '{activityName}'");

    
    public static JourneyStoryError UnsupportedEvent(string eventName) => new JourneyStoryError("UnsupportedEvent", $"Can't handle not supported event '{eventName}'");
    
    public static JourneyStoryError MissingDependency(string key) => new JourneyStoryError("MissingDependency", $"Can't find dependency '{key}'");
}