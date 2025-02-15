using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record JourneyChangedEvent(JourneyId Id, DateTimeOffset CreatedAt, DomainVersion Version)
    : DomainEvent(CreatedAt, Version);