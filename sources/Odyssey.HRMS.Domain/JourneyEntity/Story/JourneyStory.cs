using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public sealed class JourneyStory : AggregateRoot<JourneyStoryId, JourneyStoryState>
{
    private JourneyStory(JourneyStoryId id, JourneyStoryState state, IReadOnlyCollection<DomainEvent> domainEvents)
        : base(id, state, domainEvents) { }

    public static JourneyStory Create(JourneyStoryId id, IReadOnlyCollection<DomainEvent> domainEvents)
    {
        var state = JourneyStoryState.Create();
        
        return new JourneyStory(id, state, domainEvents);
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

