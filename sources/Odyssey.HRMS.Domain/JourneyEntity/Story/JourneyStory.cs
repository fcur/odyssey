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

    public static JourneyStory Create(JourneyStoryId id, IReadOnlyCollection<JourneyStoryChangedEvent> domainEvents)
    {
        var state = JourneyStoryState.Create();

        return new JourneyStory(id, state, domainEvents);
    }

    public static JourneyStory Create(JourneyStoryId id)
    {
        return Create(id, Array.Empty<JourneyStoryChangedEvent>());
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
        var eventName = storyEvent.Name;
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        var activityName = context.Name;
        
        var completedEvent = new JourneyStoryStartedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        State.Apply(completedEvent);
        AddEvent(completedEvent);
        
        return Maybe<JourneyStoryError>.None;
    }
    
    private Maybe<JourneyStoryError> HandleActionAndMoveNext(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var activityId = storyEvent.Id;
        var eventName = storyEvent.Name;
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        var activityName = context.Name;
        
        var completedEvent = new JourneyStoryCompletedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        State.Apply(completedEvent);
        AddEvent(completedEvent);
        
        return Maybe<JourneyStoryError>.None;
    }
    
    private Maybe<JourneyStoryError> HandleSourceActivityAndStartStory(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var activityId = storyEvent.Id;
        var eventName = storyEvent.Name;
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        var activityName = context.Name;
        
        var completedEvent = new JourneyStoryStartedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        State.Apply(completedEvent);
        AddEvent(completedEvent);
        
        return Maybe<JourneyStoryError>.None;
    }
    
    private Maybe<JourneyStoryError> HandleCompletionActivityAndStopStory(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var activityId = storyEvent.Id;
        var eventName = storyEvent.Name;
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        var activityName = context.Name;
        
        var completedEvent = new JourneyStoryCompletedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        State.Apply(completedEvent);
        AddEvent(completedEvent);

        return StartNextActivity(storyEvent, context, atTime);
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

public sealed record ActivityDataKey(string Key, JourneyActivityName ActivityName, JourneyActivityEventName EventName)
{
    public override string ToString() => $"{ActivityName}:{EventName}:{Key}";

    public override int GetHashCode()
    {
        return base.GetHashCode() + EqualityComparer<string>.Default.GetHashCode(Key) 
                                  + EqualityComparer<string>.Default.GetHashCode(EventName.Value)
                                  + EqualityComparer<string>.Default.GetHashCode(ActivityName.Value);
    }
}


public sealed class JourneyStoryState : AggregateRootState
{
    private Dictionary<ActivityDataKey, JsonElement> _data { get; } = new Dictionary<ActivityDataKey, JsonElement>();
    private Dictionary<JourneyActivityId, JourneyStoryActivity> _activities { get; } = new Dictionary<JourneyActivityId, JourneyStoryActivity>();
    
    public static JourneyStoryState Create() => new JourneyStoryState();
        
    protected internal override AggregateRootState Apply(DomainEvent domainEvent)
    {
        return domainEvent switch
        {
            JourneyStoryStartedEvent startedEvent => Apply(startedEvent),
            JourneyStoryCompletedEvent completedEvent => Apply(completedEvent),
            _ => throw new NotImplementedException()
        };
    }

    private AggregateRootState Apply(JourneyStoryStartedEvent startedEvent)
    {
        var activityId = startedEvent.ActivityId;
        var activity = GetOrCreateActivity(activityId, () => new JourneyStoryActivity(activityId, startedEvent.ActivityName, JourneyActivityStatus.Started));
        
        activity.Start();

        EnrichContext(startedEvent.ActivityName, startedEvent.EventName, startedEvent.Body);
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
            var dataKey = new ActivityDataKey(key, activityName, eventName);
            _data[dataKey] = eventBody.Data[key];
        }
    }

    public EmployeeId GetEmployeeId() => new EmployeeId(GetData<Guid>("EmployeeId"));

    public Guid GetTeamId() => GetData<Guid>("TeamId");

    private T? GetData<T>(ActivityDataKey activityDataKey)
    {
        return _data[activityDataKey].Deserialize<T>();
    }
    
    private T? GetData<T>(string key)
    {
        var dataKey = _data.Keys.FirstOrDefault(v => v.Key == key);
    
        if (dataKey == null)
        {
            return default;
        }
        
        return _data[dataKey].Deserialize<T>();
    }

    public bool TryGetRawData(string key, [MaybeNullWhen(false)] out JsonElement result)
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
        var dataKey = new ActivityDataKey(key, activityName, eventName);
        return _data.TryGetValue(dataKey, out result);
    }
    
}

public sealed record JourneyStoryEvent(JourneyActivityId Id, JourneyActivityEventName Name, EventBody? Body);

public sealed record JourneyStoryEventContext(JourneyActivityName Name, JourneyActivityEventType Type, JourneyActivityId? NextActivityId, IReadOnlyCollection<JourneyActivityTemplateDependency>? NextActivityDependencies);

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