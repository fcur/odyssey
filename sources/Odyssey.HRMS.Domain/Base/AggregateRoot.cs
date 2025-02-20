using System.Text.Json;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.Base;

public abstract class AggregateRoot<TId, TState> where TState : AggregateRootState
{
    protected TId Id { get; init; }
    protected TState State { get; init; }
    
    protected AggregateRoot(TId id, TState state, IReadOnlyCollection<DomainEvent> domainEvents)
    {
        Id = id;
        
        foreach (var @event in domainEvents)
        {
            State = (TState)state.Apply(@event);
        }
    }
}

public abstract class AggregateRootState
{
    private readonly Dictionary<JourneyActivityTemplateDependency, JsonElement> _data =
        new Dictionary<JourneyActivityTemplateDependency, JsonElement>();

    protected internal abstract AggregateRootState Apply(DomainEvent domainEvent);
}
