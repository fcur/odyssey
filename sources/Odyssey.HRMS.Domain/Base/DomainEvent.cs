namespace Odyssey.HRMS.Domain.Base;

public abstract record DomainEvent(DateTimeOffset CreatedAt, DomainVersion Version);