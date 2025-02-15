using System.Text.Json;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

public sealed record JourneyActivity(
    JourneyActivityId Id, 
    JourneyActivityName Name,
    JourneyActivityStatus Status,
    IReadOnlyCollection<JourneyActivityEvent> Events,
    IReadOnlyDictionary<string, JsonElement>? RawData)
    : NestedDomainEntity<JourneyActivityId>(Id) { }