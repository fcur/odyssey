using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public record JourneyActivityTemplate(
    JourneyActivityName Name,
    IReadOnlyCollection<JourneyActivityEventTemplate> Events,
    IReadOnlyCollection<JourneyActivityTemplateDependency> Dependencies,
    DateTimeOffset ChangedAt,
    DomainVersion Version,
    ulong RowVersion)
    : DomainEntity<JourneyActivityName>(Name, ChangedAt, Version)
{
    
    public static Result<JourneyActivityTemplate, JourneyActivityTemplateError> Create(
        JourneyActivityName name,
        IReadOnlyCollection<JourneyActivityEventTemplate> events,
        IReadOnlyCollection<JourneyActivityTemplateDependency> dependencies)
    {
        var changedAt = DateTimeOffset.UtcNow;
        var version = DomainVersion.New;
        var rowVersion = 0UL;

        var @event = new JourneyActivityTemplateChangedEvent(name, changedAt, version);
        var journeyActivityHeader = new JourneyActivityTemplate(name, events, dependencies, changedAt, version, rowVersion);
        journeyActivityHeader.EnqueueEvent(@event);

        return journeyActivityHeader;
    }
}