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

// ReSharper disable once ClassNeverInstantiated.Global
public sealed record JourneyActivityStartedEvent(
    JourneyStoryId StoryId,
    JourneyActivityId ActivityId,
    JourneyActivityName ActivityName,
    JourneyActivityEventName EventName,
    JourneyActivityEventType EventType,
    EventBody? Body,
    DateTimeOffset CreatedAt,
    DomainVersion Version)
    : JourneyStoryChangedEvent(StoryId, ActivityId, CreatedAt, Version);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed record JourneyActivityCompletedEvent(
    JourneyStoryId StoryId,
    JourneyActivityId ActivityId,
    JourneyActivityName ActivityName,
    JourneyActivityEventName EventName,
    JourneyActivityEventType EventType,
    EventBody? Body,
    DateTimeOffset CreatedAt,
    DomainVersion Version)
    : JourneyStoryChangedEvent(StoryId, ActivityId, CreatedAt, Version);

public sealed record EventBody(IReadOnlyDictionary<string, JsonElement> Data)
{
    public static readonly EventBody? Unset = null;

    public static EventBody Create(string key, JsonElement rawData)
    {
        var data = new Dictionary<string, JsonElement>() { { key, rawData } };
        return new EventBody(data);
    }

    public static EventBody Create()
    {
        var data = new Dictionary<string, JsonElement> { };
        return new EventBody(data);
    }

    public EventBody With<TValue>(string key, TValue value)
    {
        var newData = Data.ToDictionary();
        newData[key] = JsonSerializer.SerializeToElement(value);
        return new EventBody(newData);
    }
}