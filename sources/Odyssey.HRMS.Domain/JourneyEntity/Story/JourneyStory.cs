using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.EmployeeEntity;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;
using System.Diagnostics.CodeAnalysis;
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

    public JourneyStoryState GetState() => State;
    
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

        State.Apply(completedEvent);
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

        State.Apply(completedEvent);
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

        State.Apply(storyStartedEvent);
        AddEvent(storyStartedEvent);
        
        return StartNextActivity(storyEvent, context, atTime);
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

        State.Apply(completedEvent);
        AddEvent(completedEvent);

        return Maybe<JourneyStoryError>.None;
    }

    private Maybe<JourneyStoryError> StartNextActivity(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var nextActivityId = context.NextActivityId;
        if (nextActivityId == null)
        {
            return Maybe<JourneyStoryError>.None;
        }
        
        var storyId = Id;
        var activityId = nextActivityId;
        var activityName = JourneyActivityName.Unset;
        var eventName = JourneyActivityEventName.Unset;
        var eventType = JourneyActivityEventType.Flow;

        var eventDataResult = State.GetEventBody(context.NextActivityDependencies);

        if (eventDataResult.IsFailure)
        {
            return eventDataResult.Error;
        }

        var eventBody = new EventBody(eventDataResult.Value);
        var version = IncrementVersion();
        
        var activityStartingEvent = new JourneyStoryActivityStartingEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);
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
    
    public void EnrihWithEventResponse(JourneyActivityName activityName, JourneyActivityEventName eventName, EventBody? eventBody)
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

public sealed class JourneyStoryState : AggregateRootState
{
    private readonly JourneyStoryData _data;
    private readonly Dictionary<JourneyActivityId, JourneyStoryActivity> _activities;
    
    public EmployeeId EmployeeId { get; private set; }
    public JourneyId JourneyId { get; init; }
    
    private JourneyStoryState(JourneyId journeyId, Dictionary<JourneyActivityId, JourneyStoryActivity> activities, Dictionary<StoryDataKey, JsonElement> data)
    {
        _activities = activities;
        _data = new JourneyStoryData(data);
        JourneyId = journeyId;
    }

    public static JourneyStoryState Create(JourneyId journeyId, Dictionary<JourneyActivityId, JourneyStoryActivity> activities) 
        => Create(journeyId, activities, new Dictionary<StoryDataKey, JsonElement>());
    
    public static JourneyStoryState Create(JourneyId journeyId, Dictionary<JourneyActivityId, JourneyStoryActivity> activities, Dictionary<StoryDataKey, JsonElement> data) 
        => new JourneyStoryState(journeyId, activities, data);
        
    protected internal override AggregateRootState Apply(DomainEvent domainEvent)
    {
        return domainEvent switch
        {
            JourneyStoryActivityStartingEvent activityStartingEvent => Apply(activityStartingEvent),
            JourneyStoryStartedEvent storyStartedEvent => Apply(storyStartedEvent),
            JourneyStoryCompletedEvent storyCompletedEvent => Apply(storyCompletedEvent),
            _ => throw new NotImplementedException()
        };
    }

    private AggregateRootState Apply(JourneyStoryActivityStartingEvent activityStartingEvent)
    {
        return this;
    }
    
    private AggregateRootState Apply(JourneyStoryStartedEvent startedEvent)
    {
        var activityId = startedEvent.ActivityId;
        var activity = GetOrCreateActivity(activityId, () => new JourneyStoryActivity(activityId, startedEvent.ActivityName, JourneyActivityStatus.Started));
        
        activity.Start();

        _data.EnrihWithEventResponse(startedEvent.ActivityName, startedEvent.EventName, startedEvent.Body);
        
        var employeeIdKey = new StoryDataKey("EmployeeId", startedEvent.ActivityName, startedEvent.EventName);

        if (GetData<Guid>(employeeIdKey).TryGetValue(out var employeeIdResult))
        {
            EmployeeId = new EmployeeId(employeeIdResult);
        }
        
        return this;
    }

    private AggregateRootState Apply(JourneyStoryCompletedEvent completedEvent)
    {
        var activityId = completedEvent.ActivityId;
        var activity = GetOrCreateActivity(activityId, () => new JourneyStoryActivity(activityId, completedEvent.ActivityName, JourneyActivityStatus.Draft));
        
        activity.Finish();
        
        _data.EnrihWithEventResponse(completedEvent.ActivityName, completedEvent.EventName, completedEvent.Body);
        
        return this;
    }

    private JourneyStoryActivity GetOrCreateActivity(JourneyActivityId activityId, Func<JourneyStoryActivity> createActivity)
    {
        if (_activities.TryGetValue(activityId, out var activity))
        {
            return activity;
        }
        
        activity = createActivity();
        _activities[activityId] = activity;
        
        return activity;
    }
    
    public Result<T> GetData<T>(StoryDataKey key)
    {
        try
        {
            if (!_data.TryGetValue(key, out var data))
            {
                return Result.Failure<T>($"Data is missing for the requested key: '{key}'");
            }
            
            return data.Deserialize<T>() ?? throw new InvalidOperationException();
        }
        catch (JsonException je)
        {
            return Result.Failure<T>(je.Message);   
        }
        catch (NotSupportedException nse)
        {
            return Result.Failure<T>(nse.Message);
        }
    }
    
    // TODO: refactor
    public Result<Dictionary<string, JsonElement>, JourneyStoryError> GetEventBody(IReadOnlyCollection<JourneyActivityTemplateDependency>? activityDependencies) 
        => _data.GetEventBody(activityDependencies);
    
}

public sealed record JourneyStoryEvent(JourneyActivityId Id, JourneyActivityEventName Name, EventBody? Body);

public sealed record JourneyStoryEventContext(JourneyActivityName Name, JourneyActivityEventType Type, JourneyInitializationData? InitializationData, JourneyActivityId? NextActivityId, IReadOnlyCollection<JourneyActivityTemplateDependency>? NextActivityDependencies);

public sealed record JourneyStoryActivity(JourneyActivityId Id, JourneyActivityName ActivityName, JourneyActivityStatus Status) 
    : NestedDomainEntity<JourneyActivityId>(Id)
{
    internal JourneyActivityStatus Status { get; private set; } = Status;
    
    public void Start()
    {
        Status = JourneyActivityStatus.Started;
    }
    
    public void Finish()
    {
        Status = JourneyActivityStatus.Finished;
    }
}


public sealed record JourneyStoryError : DomainError
{
    private JourneyStoryError(string type, string message) : base(type, message) { }

    
    public static JourneyStoryError UnsupportedEvent(string eventName) => new JourneyStoryError("UnsupportedEvent", $"Can't handle not supported event '{eventName}'");
    
    public static JourneyStoryError MissingDependency(string key) => new JourneyStoryError("MissingDependency", $"Can't find dependency '{key}'");

}