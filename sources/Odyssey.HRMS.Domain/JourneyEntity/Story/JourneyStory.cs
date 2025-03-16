using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

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
        // TODO: add generic code 
        // TODO: to const
        var maybeError = context.Type.Name switch
        {
            nameof(JourneyActivityEventType.Flow) when storyEvent.Name == JourneyActivityEventName.ActivityStarted => StartActivity(storyEvent, context, atTime),
            nameof(JourneyActivityEventType.Action) => HandleActionAndMoveNext(storyEvent, context, atTime),
            nameof(JourneyActivityEventType.Source) => HandleSourceActivityAndStartStory(storyEvent, context, atTime),
            nameof(JourneyActivityEventType.Completion) => HandleCompletionActivityAndStopStory(storyEvent, context, atTime),
            _ => JourneyStoryError.UnsupportedEvent(storyEvent.Name.Value)
        };

        return maybeError; 
    }

    private Maybe<JourneyStoryError> StartActivity(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var activityId = storyEvent.Id;
        var activityName = context.Name;
        var eventName = storyEvent.Name;
        
        var activityResult = State.GetActivity(activityId);
        if (!activityResult.TryGetValue(out var activity))
        {
            return JourneyStoryError.Validation(activityResult.Error);
        }
        
        if (!activity.CouldBeStarted)
        {
            return JourneyStoryError.UnsupportedActivityAction(activityName.Value, eventName.Value);
        }
        
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        
        var startedEvent = new JourneyStoryActivityStartedEvent(Id, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        ApplyState(startedEvent);
        AddEvent(startedEvent);
        
        return Maybe<JourneyStoryError>.None;
    }
    
    private Maybe<JourneyStoryError> HandleActionAndMoveNext(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var activityId = storyEvent.Id;
        var activityName = context.Name;
        var eventName = storyEvent.Name;
        
        var activityResult = State.GetActivity(activityId);
        if (!activityResult.TryGetValue(out var activity))
        {
            return JourneyStoryError.Validation(activityResult.Error);
        }
        
        if (!activity.CouldBeFinished)
        {
            return JourneyStoryError.UnsupportedActivityAction(activityName.Value, eventName.Value);
        }
        
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        
        var completedEvent = new JourneyStoryActivityCompletedEvent(Id, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        ApplyState(completedEvent);
        AddEvent(completedEvent);
        
        return StartNextActivity(context, atTime);
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
        
        var storyActivityCompletedEvent = new JourneyStoryActivityCompletedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        ApplyState(storyActivityCompletedEvent);
        AddEvent(storyActivityCompletedEvent);
        
        return StartNextActivity(context, atTime);
    }
    
    private Maybe<JourneyStoryError> StartNextActivity(JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var nextActivityId = context.NextActivityId;
        if (nextActivityId == null)
        {
            return Maybe<JourneyStoryError>.None;
        }

        if (context.NextActivityName == JourneyActivityName.EndOfJourney)
        {
            return FinishStory(context, atTime);
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
    
    private Maybe<JourneyStoryError> FinishStory(JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var nextActivityId = context.NextActivityId;
        var activityId = nextActivityId;
        var activityName = JourneyActivityName.EndOfJourney;
        var eventName = JourneyActivityEventName.ActivityCompleted;
        var eventType = JourneyActivityEventType.Completion;
        var eventBody = StoryEventBody.Unset;
        var version = IncrementVersion();

        var storyCompletedEvent = new JourneyStoryCompletedEvent(storyId, activityId!, activityName, eventName, eventType, eventBody, atTime, version);

        ApplyState(storyCompletedEvent);
        AddEvent(storyCompletedEvent);
        
        return Maybe<JourneyStoryError>.None;
    }
}


public sealed record JourneyStoryEvent(JourneyActivityId Id, JourneyActivityEventName Name, StoryEventBody? Body);

public sealed record JourneyStoryEventContext(JourneyActivityName Name, JourneyActivityEventType Type, JourneyActivityId? NextActivityId, JourneyActivityName? NextActivityName,  IReadOnlyCollection<JourneyActivityTemplateDependency>? NextActivityDependencies);