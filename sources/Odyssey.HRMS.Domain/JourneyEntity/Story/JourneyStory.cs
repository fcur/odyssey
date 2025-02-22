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

    public Maybe<DomainError> Handle(JourneyStoryEvent @event,JourneyStoryEventContext context, DateTimeOffset atTime)
    {
        return Maybe<DomainError>.None;
    }
}

public sealed class JourneyStoryState : AggregateRootState
{
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
        return this;
    }

    private AggregateRootState Apply(JourneyActivityCompletedEvent completedEvent)
    {
        return this;
    }
}

public sealed record JourneyStoryEvent(JourneyActivityId Id, JourneyActivityEventName Name, EventBody? Body);

public sealed record JourneyStoryEventContext(JourneyActivityName Name, JourneyActivityEventType Type, JourneyActivityId? NextActivityId, IReadOnlyCollection<JourneyActivityTemplateDependency>? NextActivityDependencies);