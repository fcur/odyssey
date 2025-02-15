using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public sealed record JourneyActivityTemplateChangedEvent( JourneyActivityName Name, DateTimeOffset CreatedAt, DomainVersion Version)
    : DomainEvent(CreatedAt, Version);