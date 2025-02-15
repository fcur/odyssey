using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record JourneyValidationError : DomainError
{
    private JourneyValidationError(string type, string message) : base(type, message) { }

    public static JourneyValidationError MissingSourceActivity(JourneyId journeyId) => new JourneyValidationError("InvalidActivities", $"Journey '{journeyId}' must contain at least one activity with 'source' event type.");
}