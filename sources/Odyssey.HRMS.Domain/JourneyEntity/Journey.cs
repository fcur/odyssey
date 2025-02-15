using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record Journey(
    JourneyId Id,
    JourneyName Name,
    IReadOnlyCollection<JourneyActivity> Activities,
    JourneyStatus Status,
    JourneyStartup? Startup,
    DateTimeOffset ChangedAt,
    DomainVersion Version,
    ulong RowVersion)
    : DomainEntity<JourneyId>(Id, ChangedAt, Version)
{
    public static Result<Journey, JourneyValidationError> Create(
        JourneyName name,
        IReadOnlyCollection<JourneyActivity> activities,
        JourneyStartup? startup)
    {
        var id = JourneyId.New();
        var status = JourneyStatus.Draft;
        var changedAt = DateTimeOffset.UtcNow;
        var version = DomainVersion.New;
        var rowVersion = 0UL;

        // warn: 'StartAt' field is required for 'Ready' journeys.
        // > check it on journey status update
        
        var @event = new JourneyChangedEvent(id, changedAt, version);
        var journey = new Journey(id, name, activities, status, startup, changedAt, version, rowVersion);
        journey.EnqueueEvent(@event);

        return journey;
    }
}