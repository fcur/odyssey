using System.Collections.Immutable;

namespace Odyssey.HRMS.Domain.Base;

public abstract class AggregateRoot<TId, TState, TDomainEvent> where TState : AggregateRootState<TDomainEvent> where TDomainEvent : DomainEvent
{
    private readonly List<TDomainEvent> _events;
    protected TId Id { get; }
    protected TState State { get; }
    public DomainVersion Version { get; private set; }
    
    protected AggregateRoot(TId id, TState state, IReadOnlyCollection<TDomainEvent> domainEvents)
    {
        Id = id;
        Version = DomainVersion.New;
        
        foreach (var @event in domainEvents)
        {
            state.Apply(@event);
            IncrementVersion();
        }

        _events = new List<TDomainEvent>(domainEvents);
        State = state;
    }

    protected DomainVersion IncrementVersion()
    {
        return ++Version;
    }

    protected void AddEvent(TDomainEvent @event)
    {
        _events.Add(@event);
    }

    protected void ApplyState(TDomainEvent @event)
    {
        State.Apply(@event);
    }
    
    public ImmutableArray<DomainEvent> GetEvents() => [.._events];
}

public abstract class AggregateRootState<TDomainEvent> where TDomainEvent : DomainEvent
{
    protected internal abstract void Apply(TDomainEvent domainEvent);
}
