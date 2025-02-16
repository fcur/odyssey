using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record JourneyValidationError : DomainError
{
    private JourneyValidationError(string type, string message) : base(type, message) { }

    public static readonly JourneyValidationError MissingSourceActivity = new JourneyValidationError("InvalidActivities", $"Journey must contain at least one activity with '{JourneyActivityEventType.Source}' event type.");
    public static readonly JourneyValidationError IncorrectEndOfJourneyActivity = new JourneyValidationError("InvalidActivities", $"Journey must contain exact one activity with '{JourneyActivityName.EndOfJourney}' name.");
    public static JourneyValidationError ActivityIdDuplicate(JourneyActivityId id) => new JourneyValidationError("InvalidActivities", $"Journey must contain activities with unique identifiers, need to check activity: '{id}'.");


}