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
            nameof(JourneyActivityEventType.Source) => HandleSourceActivityAndStartStory(storyEvent, context, atTime),
            nameof(JourneyActivityEventType.Action) => HandleActionAndMoveNext(storyEvent, context, atTime),
            nameof(JourneyActivityEventType.Exit) => HandleFinalActivityAndStopStory(storyEvent, context, atTime),
            _ => JourneyStoryError.UnsupportedEvent(storyEvent.Name.Value)
        };

        return maybeError;
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
    
    private Maybe<JourneyStoryError> HandleFinalActivityAndStopStory(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
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
}

public sealed record ActivityDataKey(string Key, JourneyActivityName ActivityName, JourneyActivityEventName EventName);


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
    
}