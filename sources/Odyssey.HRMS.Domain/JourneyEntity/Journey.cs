using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record Journey(
    JourneyId Id,
    JourneyName Name,
    IReadOnlyCollection<JourneyActivity> Activities,
    JourneyStatus Status,
    JourneyStartup? Startup,
    JourneyInitializationData? InitializationData,
    DateTimeOffset ChangedAt,
    DomainVersion Version,
    ulong RowVersion)
    : DomainEntity<JourneyId>(Id, ChangedAt, Version)
{
    public static Result<Journey, JourneyValidationError> Create(
        JourneyName name,
        IReadOnlyCollection<JourneyActivity> activities,
        JourneyStartup? startup = null,
        JourneyInitializationData? initializationData = null)
    {

        var sourceActivityEvents = activities.SelectMany(v => v.Events)
            .Where(v => v.EventType == JourneyActivityEventType.Source).ToArray();

        if (sourceActivityEvents.Length == 0)
        {
            return JourneyValidationError.MissingSourceActivity;
        }
        
        var id = JourneyId.New();
        var status = JourneyStatus.Draft;
        var changedAt = DateTimeOffset.UtcNow;
        var version = DomainVersion.New;
        var rowVersion = 0UL;

        // warn: 'StartAt' field is required for 'Ready' journeys.
        // > check it on journey status update
        
        var @event = new JourneyChangedEvent(id, changedAt, version);
        var journey = new Journey(id, name, activities, status, startup, initializationData, changedAt, version, rowVersion);
        journey.EnqueueEvent(@event);

        return journey;
    }
}