using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record JourneyValidationError : DomainError
{
    private JourneyValidationError(string type, string message) : base(type, message) { }

    public static readonly JourneyValidationError MissingSourceActivity = new JourneyValidationError("InvalidActivities", "Journey must contain at least one activity with 'source' event type.");
}