using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.EmployeeEntity;

public sealed record EmployeeChangedEvent(EmployeeId Id, DateTimeOffset CreatedAt, DomainVersion Version)
    : DomainEvent(CreatedAt, Version);