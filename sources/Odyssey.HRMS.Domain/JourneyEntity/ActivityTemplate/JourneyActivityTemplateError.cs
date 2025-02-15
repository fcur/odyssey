using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public sealed record JourneyActivityTemplateError : DomainError
{
    private JourneyActivityTemplateError(string type, string message) : base(type, message)
    {
    }
}