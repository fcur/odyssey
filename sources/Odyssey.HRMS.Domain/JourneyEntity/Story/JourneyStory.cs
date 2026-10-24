using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public sealed class JourneyStory : AggregateRoot<JourneyStoryId, JourneyStoryState, JourneyStoryChangedEvent>
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
        var maybeError = context.Type switch
        {
            SourceJourneyActivityEventType => HandleSourceActivityAndStartStory(storyEvent, context, atTime),
            FlowJourneyActivityEventType when storyEvent.Name == JourneyActivityEventName.ActivityStarted 
                => StartActivity(storyEvent, context, atTime),
            SuccessJourneyActivityEventType => HandleSuccessfulResultsAndMoveNext(storyEvent, context, atTime),
            // TODO: handle timeout events like failure results
            FailJourneyActivityEventType => HandleFailureResultsAndMoveNext(storyEvent, context, atTime),
            ExitJourneyActivityEventType => HandleExitAndCompleteStory(storyEvent, context, atTime),
            _ => JourneyStoryError.UnsupportedEvent(storyEvent.Name.Value, context.Type.Value)
        };

        return maybeError;
    }
    
    private Maybe<JourneyStoryError> HandleSourceActivityAndStartStory(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        if (State.IsFinished)
        {
            return JourneyStoryError.Validation("Can't start finished story");
        }
        
        var activityId = storyEvent.Id;
        
        var activityResult = State.GetActivity(activityId);
        if (!activityResult.TryGetValue(out var activity))
        {
            return JourneyStoryError.Validation(activityResult.Error);
        }
        
        if(activity.IsFinished)
        {
            return JourneyStoryError.Validation("Can't start finished activity");
        }
        
        var storyId = Id;
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

        if (activity.IsStarted || activity.IsFinished)
        {
            return Maybe<JourneyStoryError>.None;
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
    
    private Maybe<JourneyStoryError> HandleSuccessfulResultsAndMoveNext(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var activityId = storyEvent.Id;
        
        var activityResult = State.GetActivity(activityId);
        if (!activityResult.TryGetValue(out var activity))
        {
            return JourneyStoryError.Validation(activityResult.Error);
        }

        if (activity.IsFinished)
        {
            return Maybe<JourneyStoryError>.None;
        }
        
        var activityName = context.Name;
        var eventName = storyEvent.Name;
        
        if (!activity.CanBeFinished)
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
    
    private Maybe<JourneyStoryError> HandleFailureResultsAndMoveNext(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var activityId = storyEvent.Id;
        
        var activityResult = State.GetActivity(activityId);
        if (!activityResult.TryGetValue(out var activity))
        {
            return JourneyStoryError.Validation(activityResult.Error);
        }
        
        var activityName = context.Name;
        var eventName = storyEvent.Name;
        
        if (!activity.CanBeFinished)
        {
            return JourneyStoryError.UnsupportedActivityAction(activityName.Value, eventName.Value);
        }
        
        var storyId = Id;
        var eventType = context.Type;
        var eventBody = storyEvent.Body;
        var version = IncrementVersion();
        
        var completedEvent = new JourneyStoryActivityCompletedEvent(storyId, activityId, activityName, eventName, eventType, eventBody, atTime, version);

        ApplyState(completedEvent);
        AddEvent(completedEvent);
        
        return StartNextActivity(context, atTime);
    }
    
    private Maybe<JourneyStoryError> HandleExitAndCompleteStory(JourneyStoryEvent storyEvent, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var activityId = storyEvent.Id;
        return FinishStory(activityId, context, atTime);
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
            return FinishStory(nextActivityId, context, atTime);
        }
        
        var storyId = Id;
        var activityId = nextActivityId;
        var activityName = context.NextActivityName;
        var eventType = context.Type;
        var eventDataResult = State.GetEventBody(context.NextActivityDependencies);
        if (eventDataResult.IsFailure)
        {
            return eventDataResult.Error;
        }

        var activityData = new StoryActivityData(eventDataResult.Value);
        var version = IncrementVersion();
        
        var activityStartingEvent = new JourneyStoryActivityStartingEvent(storyId, activityId, activityName!, eventType, activityData, atTime, version);
        
        ApplyState(activityStartingEvent);
        AddEvent(activityStartingEvent);
        
        return Maybe<JourneyStoryError>.None;
    }
    
    private Maybe<JourneyStoryError> FinishStory(JourneyActivityId activityId, JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        var storyId = Id;
        var activityName = JourneyActivityName.EndOfJourney;
        var eventName = JourneyActivityEventName.ActivityCompleted;
        var eventType = JourneyActivityEventType.Exit;
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