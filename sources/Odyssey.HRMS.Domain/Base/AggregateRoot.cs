using System.Text.Json;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.Base;

public abstract class AggregateRoot<TId, TState> where TState : AggregateRootState
{
    protected TId Id { get; init; }
    protected TState State { get; init; }
    protected DomainVersion Version { get; private set; }
    protected List<DomainEvent> Events { get; } = new();
    
    protected AggregateRoot(TId id, TState state, IReadOnlyCollection<DomainEvent> domainEvents)
    {
        Id = id;
        Version = DomainVersion.New;
        
        foreach (var @event in domainEvents)
        {
            state = (TState)state.Apply(@event);
            Version++;
        }

        State = state;
    }

    protected DomainVersion IncrementVersion()
    {
        return Version++;
    }

    protected void AddEvent(DomainEvent @event)
    {
        Events.Add(@event);
    }
}

public abstract class AggregateRootState
{
    private readonly Dictionary<JourneyActivityTemplateDependency, JsonElement> _data =
        new Dictionary<JourneyActivityTemplateDependency, JsonElement>();

    protected internal abstract AggregateRootState Apply(DomainEvent domainEvent);
}
