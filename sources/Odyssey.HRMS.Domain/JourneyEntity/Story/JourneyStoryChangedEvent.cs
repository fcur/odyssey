using System.Text.Json;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.EmployeeEntity;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public abstract record JourneyStoryChangedEvent(
    JourneyStoryId StoryId,
    JourneyActivityId ActivityId,
    DateTimeOffset CreatedAt,
    DomainVersion Version)
    : DomainEvent(CreatedAt, Version);

public sealed record JourneyStoryActivityStartingEvent(
    JourneyStoryId StoryId,
    JourneyActivityId ActivityId,
    JourneyActivityName ActivityName,
    JourneyActivityEventType EventType,
    StoryActivityData ActivityData,
    DateTimeOffset CreatedAt,
    DomainVersion Version)
    : JourneyStoryChangedEvent(StoryId, ActivityId, CreatedAt, Version);

public sealed record JourneyStoryActivityStartedEvent(
    JourneyStoryId StoryId,
    JourneyActivityId ActivityId,
    JourneyActivityName ActivityName,
    JourneyActivityEventName EventName,
    JourneyActivityEventType EventType,
    StoryEventBody? Body,
    DateTimeOffset CreatedAt,
    DomainVersion Version)
    : JourneyStoryChangedEvent(StoryId, ActivityId, CreatedAt, Version);

public sealed record JourneyStoryActivityCompletedEvent(
    JourneyStoryId StoryId,
    JourneyActivityId ActivityId,
    JourneyActivityName ActivityName,
    JourneyActivityEventName EventName,
    JourneyActivityEventType EventType,
    StoryEventBody? Body,
    DateTimeOffset CreatedAt,
    DomainVersion Version)
    : JourneyStoryChangedEvent(StoryId, ActivityId, CreatedAt, Version);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed record JourneyStoryStartedEvent(
    JourneyStoryId StoryId,
    JourneyActivityId ActivityId,
    JourneyActivityName ActivityName,
    JourneyActivityEventName EventName,
    JourneyActivityEventType EventType,
    StoryEventBody? EventBody,
    DateTimeOffset CreatedAt,
    DomainVersion Version)
    : JourneyStoryChangedEvent(StoryId, ActivityId, CreatedAt, Version);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed record JourneyStoryCompletedEvent(
    JourneyStoryId StoryId,
    JourneyActivityId ActivityId,
    JourneyActivityName ActivityName,
    JourneyActivityEventName EventName,
    JourneyActivityEventType EventType,
    StoryEventBody? Body,
    DateTimeOffset CreatedAt,
    DomainVersion Version)
    : JourneyStoryChangedEvent(StoryId, ActivityId, CreatedAt, Version);

public sealed record StoryEventBody(IReadOnlyDictionary<string, JsonElement> Data)
{
    public static readonly StoryEventBody? Unset = null;
}


public sealed record StoryActivityData(IReadOnlyDictionary<string, JsonElement> Data);