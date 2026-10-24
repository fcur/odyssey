using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.Domain.Base;

public abstract record DomainEntity<TId, TDomainEvent>()
{
    private readonly Queue<TDomainEvent> _domainEvents;
    public TId Id { get; init; }
    public DateTimeOffset ChangedAt { get; set; }
    public DomainVersion Version { get; set; }

    protected DomainEntity(TId id, DateTimeOffset changedAt, DomainVersion version) : this()
    {
        Id = id;
        ChangedAt = changedAt;
        Version = version;
        _domainEvents = new Queue<TDomainEvent>();
    }

    protected void EnqueueEvent(TDomainEvent domainEvent)
    {
        _domainEvents.Enqueue(domainEvent);
    }

    public bool TryDequeueEvent([MaybeNullWhen(false)] out TDomainEvent domainEvent)
    {
        return _domainEvents.TryDequeue(out domainEvent);
    }
}