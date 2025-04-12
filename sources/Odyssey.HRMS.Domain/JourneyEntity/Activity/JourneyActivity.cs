using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

public sealed record JourneyActivity(
    JourneyActivityId Id,
    JourneyActivityName Name,
    JourneyActivityStatus Status,
    IReadOnlyCollection<JourneyActivityEvent> Events)
    : NestedDomainEntity<JourneyActivityId>(Id)
{
    public static Result<JourneyActivity> Create(
        JourneyActivityId id,
        JourneyActivityName name,
        JourneyActivityStatus status,
        IReadOnlyCollection<JourneyActivityEvent> events)
    {
        var result = new JourneyActivity(id, name, status, events);
        return result;
    }

    public static JourneyActivity CreateEndOfJourney(JourneyActivityId id)
    {
        var status = JourneyActivityStatus.Draft;
        var events = Array.Empty<JourneyActivityEvent>();
        return new JourneyActivity(id, JourneyActivityName.EndOfJourney, status, events);
    }
}