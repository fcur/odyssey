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
        
        var completedEvent = new JourneyStoryStartedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        State.Apply(completedEvent);
        AddEvent(completedEvent);
        
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
        
        var eventBody = EventBody.Create();

        // TODO: refactor
        // var storyData = new JourneyStoryData(_data);
        // storyData.GetEventBody(context.NextActivityDependencies);
        
        if (context.NextActivityDependencies != null)
        {
            foreach (var dependency in context.NextActivityDependencies)
            {
                var key = dependency.Key;
                var source = dependency.Source;

                if (source != null && !State.TryGetRawData(key, source.ActivityName, source.EventName, out var dependencyData)||
                    !State.TryGetRawData(key, out dependencyData))
                {
                    return JourneyStoryError.MissingDependency(key);
                }

                eventBody.With(key, dependencyData);
            }
        }
        
        var version = IncrementVersion();
        
        var startingEvent = new JourneyStoryStartingEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);
        AddEvent(startingEvent);
        
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
    public EventBody GetEventBody(IReadOnlyCollection<JourneyActivityTemplateDependency>? activityDependencies)
    {
        return EventBody.Create();
    }
}

public sealed class JourneyStoryState : AggregateRootState
{
    private readonly Dictionary<StoryDataKey, JsonElement> _data;
    private readonly Dictionary<JourneyActivityId, JourneyStoryActivity> _activities;
    
    public EmployeeId EmployeeId { get; private set; }
    public JourneyId JourneyId { get; init; }
    
    private JourneyStoryState(JourneyId journeyId, Dictionary<JourneyActivityId, JourneyStoryActivity> activities, Dictionary<StoryDataKey, JsonElement> data)
    {
        _activities = activities;
        _data = data;
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
            JourneyStoryStartingEvent startingEvent => Apply(startingEvent),
            JourneyStoryStartedEvent startedEvent => Apply(startedEvent),
            JourneyStoryCompletedEvent completedEvent => Apply(completedEvent),
            _ => throw new NotImplementedException()
        };
    }

    private AggregateRootState Apply(JourneyStoryStartingEvent startingEvent)
    {
        return this;
    }
    
    private AggregateRootState Apply(JourneyStoryStartedEvent startedEvent)
    {
        var activityId = startedEvent.ActivityId;
        var activity = GetOrCreateActivity(activityId, () => new JourneyStoryActivity(activityId, startedEvent.ActivityName, JourneyActivityStatus.Started));
        
        activity.Start();

        EnrichContext(startedEvent.ActivityName, startedEvent.EventName, startedEvent.Body);

        var employeeIdDataKey = new StoryDataKey("EmployeeId", startedEvent.ActivityName, startedEvent.EventName);

        if (GetData<Guid>(employeeIdDataKey).TryGetValue(out var employeeIdResult))
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
        
        EnrichContext(completedEvent.ActivityName, completedEvent.EventName, completedEvent.Body);
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
    
    private void EnrichContext(JourneyActivityName activityName, JourneyActivityEventName eventName, EventBody? eventBody)
    {
        if (eventBody == null)
        {
            return;
        }
        
        foreach (var key in eventBody.Data.Keys)
        {
            var dataKey = new StoryDataKey(key, activityName, eventName);
            _data[dataKey] = eventBody.Data[key];
        }
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
    
    public bool TryGetRawData(string key, out JsonElement result)
    {
        var dataKey = _data.Keys.FirstOrDefault(v => v.Key == key);

        if (dataKey == null)
        {
            result = default;
            return false;
        }
        
        return _data.TryGetValue(dataKey, out result);
    }
    
    public bool TryGetRawData(string key, JourneyActivityName activityName, JourneyActivityEventName eventName, [MaybeNullWhen(false)] out JsonElement result)
    {
        var dataKey = new StoryDataKey(key, activityName, eventName);
        return _data.TryGetValue(dataKey, out result);
    }
    
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