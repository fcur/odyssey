using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.Domain.Base;

public abstract record DomainEntity<TId>()
{
    private readonly Queue<DomainEvent> _domainEvents;
    public TId Id { get; init; }
    protected DateTimeOffset ChangedAt { get; set; }
    protected DomainVersion Version { get; set; }

    protected DomainEntity(TId id, DateTimeOffset changedAt, DomainVersion version) : this()
    {
        Id = id;
        ChangedAt = changedAt;
        Version = version;
        _domainEvents = new Queue<DomainEvent>();
    }

    protected void EnqueueEvent(DomainEvent domainEvent)
    {
        _domainEvents.Enqueue(domainEvent);
    }

    protected bool TryDequeueEvent([MaybeNullWhen(false)] out DomainEvent domainEvent)
    {
        return _domainEvents.TryDequeue(out domainEvent);
    }
}