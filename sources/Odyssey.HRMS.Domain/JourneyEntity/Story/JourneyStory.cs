using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public sealed class JourneyStory : AggregateRoot<JourneyStoryId, JourneyStoryState>
{
    private JourneyStory(JourneyStoryId id, JourneyStoryState state, IReadOnlyCollection<DomainEvent> domainEvents)
        : base(id, state, domainEvents)
    {
    }

    public static JourneyStory Create(JourneyStoryId id, IReadOnlyCollection<DomainEvent> domainEvents)
    {
        var state = JourneyStoryState.Create();

        return new JourneyStory(id, state, domainEvents);
    }

    public static JourneyStory Create(JourneyStoryId id)
    {
        return Create(id, Array.Empty<DomainEvent>());
    }

    public Maybe<DomainError> Handle(JourneyStoryEvent @event, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var maybeError = context.Type.Name switch
        {
            nameof(JourneyActivityEventType.Source) => HandleSourceActivityEventCompleted(@event, context, atTime),
            _ => throw new InvalidOperationException()
        };

        return maybeError;
    }

    private Maybe<DomainError> HandleSourceActivityEventCompleted(JourneyStoryEvent @event, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var activityId = @event.Id;
        var eventName = @event.Name;
        var eventType = context.Type;
        var eventBody = @event.Body;
        var version = IncrementVersion();
        var activityName = context.Name;
        
        var completedEvent = new JourneyActivityCompletedEvent(storyId, activityId, activityName, eventName, eventType,eventBody, atTime, version);

        State.Apply(completedEvent);
        AddEvent(completedEvent);
        
        return Maybe<DomainError>.None;
    }
}

public sealed class JourneyStoryState : AggregateRootState
{
    public Dictionary<JourneyActivityId, JourneyStoryActivity> Activities { get; } = new Dictionary<JourneyActivityId, JourneyStoryActivity>();
    
    public static JourneyStoryState Create() => new JourneyStoryState();
    
    
    protected internal override AggregateRootState Apply(DomainEvent domainEvent)
    {
        return domainEvent switch
        {
            JourneyActivityStartedEvent startedEvent => Apply(startedEvent),
            JourneyActivityCompletedEvent completedEvent => Apply(completedEvent),
            _ => throw new NotImplementedException()
        };
    }

    private AggregateRootState Apply(JourneyActivityStartedEvent startedEvent)
    {
        var activityId = startedEvent.ActivityId;
        var activity = GetOrCreateActivity(activityId, () => new JourneyStoryActivity(activityId, startedEvent.ActivityName, JourneyActivityStatus.Started));
        
        activity.Start();
        return this;
    }

    private AggregateRootState Apply(JourneyActivityCompletedEvent completedEvent)
    {
        var activityId = completedEvent.ActivityId;
        var activity = GetOrCreateActivity(activityId, () => new JourneyStoryActivity(activityId, completedEvent.ActivityName, JourneyActivityStatus.Draft));
        
        activity.Finish();
        return this;
    }

    private JourneyStoryActivity GetOrCreateActivity(JourneyActivityId activityId, Func<JourneyStoryActivity> createActivity)
    {
        if (Activities.TryGetValue(activityId, out var activity))
        {
            return activity;
        }
        
        activity = createActivity();
        Activities[activityId] = activity;
        
        return activity;
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
