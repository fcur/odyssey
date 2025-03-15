using System.Collections.Immutable;

namespace Odyssey.HRMS.Domain.Base;

public abstract class AggregateRoot<TId, TState> where TState : AggregateRootState
{
    protected TId Id { get; }
    protected TState State { get; }
    public DomainVersion Version { get; private set; }
    private Queue<DomainEvent> Events { get; } = new();
    
    protected AggregateRoot(TId id, TState state, IReadOnlyCollection<DomainEvent> domainEvents)
    {
        Id = id;
        Version = DomainVersion.New;
        
        foreach (var @event in domainEvents)
        {
            state.Apply(@event);
            IncrementVersion();
        }

        Events = new Queue<DomainEvent>(domainEvents);
        State = state;
    }

    protected DomainVersion IncrementVersion()
    {
        return ++Version;
    }

    protected void AddEvent(DomainEvent @event)
    {
        Events.Enqueue(@event);
    }

    protected void ApplyState(DomainEvent @event)
    {
        State.Apply(@event);
    }
    
    public ImmutableArray<DomainEvent> GetEvents() => [..Events];
}

public abstract class AggregateRootState
{
    protected internal abstract void Apply(DomainEvent domainEvent);
}
