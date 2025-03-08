using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;
using System.Text.Json;

namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public sealed class JourneyStory : AggregateRoot<JourneyStoryId, JourneyStoryState>
{
    private JourneyStory(JourneyStoryId id, JourneyStoryState state, IReadOnlyCollection<JourneyStoryChangedEvent> domainEvents)
        : base(id, state, domainEvents)
    {
    }

    public static JourneyStory Create(JourneyStoryId id, JourneyStoryState state, IReadOnlyCollection<JourneyStoryChangedEvent> domainEvents)
    {
        return new JourneyStory(id, state, domainEvents);
    }

    public static JourneyStory Create(JourneyStoryId id, JourneyStoryState state)
    {
        return Create(id, state, Array.Empty<JourneyStoryChangedEvent>());
    }

    public Maybe<JourneyStoryError> Handle(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        // add generic code 
        
        var maybeError = context.Type.Name switch
        {
            // TBD: to const
            nameof(JourneyActivityEventType.Flow) when storyEvent.Name == JourneyActivityEventName.ActivityStarted => StartActivity(storyEvent, context, atTime),
            nameof(JourneyActivityEventType.Source) => HandleSourceActivityAndStartStory(storyEvent, context, atTime),
            nameof(JourneyActivityEventType.Action) => HandleActionAndMoveNext(storyEvent, context, atTime),
            nameof(JourneyActivityEventType.Completion) => HandleCompletionActivityAndStopStory(storyEvent, context, atTime),
            _ => JourneyStoryError.UnsupportedEvent(storyEvent.Name.Value)
        };

        return maybeError;
    }

    private Maybe<JourneyStoryError> StartActivity(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var activityId = storyEvent.Id;
        var activityName = context.Name;
        var eventName = storyEvent.Name;
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        
        var completedEvent = new JourneyStoryStartedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        ApplyState(completedEvent);
        AddEvent(completedEvent);
        
        return Maybe<JourneyStoryError>.None;
    }
    
    private Maybe<JourneyStoryError> HandleActionAndMoveNext(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var activityId = storyEvent.Id;
        var activityName = context.Name;
        var eventName = storyEvent.Name;
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        
        var completedEvent = new JourneyStoryCompletedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        ApplyState(completedEvent);
        AddEvent(completedEvent);
        
        return Maybe<JourneyStoryError>.None;
    }
    
    private Maybe<JourneyStoryError> HandleSourceActivityAndStartStory(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var activityId = storyEvent.Id;
        var activityName = context.Name;
        var eventName = storyEvent.Name;
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        
        var storyStartedEvent = new JourneyStoryStartedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        ApplyState(storyStartedEvent);
        AddEvent(storyStartedEvent);
        
        return StartNextActivity(context, atTime);
    }
    
    private Maybe<JourneyStoryError> HandleCompletionActivityAndStopStory(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var activityId = storyEvent.Id;
        var activityName = context.Name;
        var eventName = storyEvent.Name;
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        
        var completedEvent = new JourneyStoryCompletedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        ApplyState(completedEvent);
        AddEvent(completedEvent);

        return Maybe<JourneyStoryError>.None;
    }

    private Maybe<JourneyStoryError> StartNextActivity(JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var nextActivityId = context.NextActivityId;
        if (nextActivityId == null)
        {
            return Maybe<JourneyStoryError>.None;
        }
        
        var storyId = Id;
        var activityId = nextActivityId;
        var activityName = context.NextActivityName;

        var eventDataResult = State.GetEventBody(context.NextActivityDependencies);
        if (eventDataResult.IsFailure)
        {
            return eventDataResult.Error;
        }

        var activityData = new StoryActivityData(eventDataResult.Value);
        var version = IncrementVersion();
        
        var activityStartingEvent = new JourneyStoryActivityStartingEvent(storyId, activityId, activityName!, activityData, atTime, version);
        
        ApplyState(activityStartingEvent);
        AddEvent(activityStartingEvent);
        
        return Maybe<JourneyStoryError>.None;
    }
    
}

public readonly record struct StoryDataKey(string Key, JourneyActivityName? ActivityName, JourneyActivityEventName? EventName)
{
    public static StoryDataKey Create(string key) => new StoryDataKey(key, null, null);
    public static StoryDataKey Create(string key, JourneyActivityName activityName, JourneyActivityEventName eventName) => new StoryDataKey(key, activityName, eventName);

    public override string ToString()
    {
        if (ActivityName != null && EventName != null)
        {
            return $"{ActivityName}:{EventName}:{Key}";
        }
        
        return Key;
    }

    public override int GetHashCode()
    {
        var baseHashCode = base.GetHashCode();
        if (ActivityName != null && EventName != null)
        {
            return  baseHashCode + EqualityComparer<string>.Default.GetHashCode(Key) 
                                + EqualityComparer<string>.Default.GetHashCode(EventName.Value)
                                + EqualityComparer<string>.Default.GetHashCode(ActivityName.Value);
        }

        return baseHashCode + EqualityComparer<string>.Default.GetHashCode(Key);
    }
}



public sealed record JourneyStoryData(Dictionary<StoryDataKey, JsonElement> Data)
{
    public Result<Dictionary<string, JsonElement>, JourneyStoryError> GetEventBody(IReadOnlyCollection<JourneyActivityTemplateDependency>? activityDependencies)
    {
        var result = new Dictionary<string, JsonElement>();
        if (activityDependencies == null)
        {
            return result;
        }

        foreach (var item in activityDependencies)
        {
            var key = new StoryDataKey(item.Key, item.Source?.ActivityName, item.Source?.EventName);

            if (!Data.TryGetValue(key, out var jsonValue))
            {
                return JourneyStoryError.MissingDependency(item.Key);
            }

            result[item.Key] = jsonValue;
        }

        return result;
    }

    public bool TryGetValue(StoryDataKey key, out JsonElement data) => Data.TryGetValue(key, out data);
    
    public void EnrichWithEventResponse(JourneyActivityName activityName, JourneyActivityEventName eventName, StoryEventBody? eventBody)
    {
        if (eventBody == null)
        {
            return;
        }
        
        foreach (var key in eventBody.Data.Keys)
        {
            var dataKey = new StoryDataKey(key, activityName, eventName);
            Data[dataKey] = eventBody.Data[key];
        }
    }
}

public sealed record JourneyStoryEvent(JourneyActivityId Id, JourneyActivityEventName Name, StoryEventBody? Body);

public sealed record JourneyStoryEventContext(JourneyActivityName Name, JourneyActivityEventType Type, JourneyInitializationData? InitializationData, JourneyActivityId? NextActivityId, JourneyActivityName? NextActivityName,  IReadOnlyCollection<JourneyActivityTemplateDependency>? NextActivityDependencies);

public enum JourneyStoryActivityStatus: byte
{
    Ready,
    Starting,
    Started,
    Finished
}

public sealed record JourneyStoryActivity(JourneyActivityId Id, JourneyActivityName ActivityName, JourneyStoryActivityStatus Status) 
    : NestedDomainEntity<JourneyActivityId>(Id)
{
    internal JourneyStoryActivityStatus Status { get; private set; } = Status;
    
    public void SetStarted()
    {
        Status = JourneyStoryActivityStatus.Started;
    }

    public void Start()
    {
        Status = JourneyStoryActivityStatus.Starting;
    }
    
    public void SetFinished()
    {
        Status = JourneyStoryActivityStatus.Finished;
    }
}


public sealed record JourneyStoryError : DomainError
{
    private JourneyStoryError(string type, string message) : base(type, message) { }

    
    public static JourneyStoryError UnsupportedEvent(string eventName) => new JourneyStoryError("UnsupportedEvent", $"Can't handle not supported event '{eventName}'");
    
    public static JourneyStoryError MissingDependency(string key) => new JourneyStoryError("MissingDependency", $"Can't find dependency '{key}'");
}