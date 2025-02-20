using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Story;

namespace Odyssey.HRMS.Domain.EmployeeEntity;

public sealed class PaidHoliday : AggregateRoot<EmployeeId, PaidHolidayState>
{
    private PaidHoliday(EmployeeId id, PaidHolidayState state, IReadOnlyCollection<DomainEvent> domainEvents)
        : base(id, state, domainEvents) { }
    
    public static PaidHoliday Create(EmployeeId id, IReadOnlyCollection<DomainEvent> domainEvents)
    {
        var state = PaidHolidayState.Create();
        
        return new PaidHoliday(id, state, domainEvents);
    }
}

public sealed class PaidHolidayState : AggregateRootState
{
    public static PaidHolidayState Create() => new PaidHolidayState();
    protected internal override AggregateRootState Apply(DomainEvent domainEvent)
    {
        return domainEvent switch
        {
            PaidHolidayAccruedEvent accruedEvent => Apply(accruedEvent),
            PaidHolidayUsedEvent usedEvent => Apply(usedEvent),
            _ => throw new NotImplementedException()
        };
    }
    
    private AggregateRootState Apply(PaidHolidayAccruedEvent accruedEvent)
    {
        return this;
    }
    
    private AggregateRootState Apply(PaidHolidayUsedEvent usedEvent)
    {
        return this;
    }
}

public abstract record PaidHolidayEvent(EmployeeId EmployeeId, decimal Amount, EventBody? Body, DateTimeOffset CreatedAt, DomainVersion Version)
    : DomainEvent(CreatedAt, Version);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed record PaidHolidayAccruedEvent(EmployeeId EmployeeId, decimal Amount, EventBody? Body, DateTimeOffset CreatedAt, DomainVersion Version)
    : PaidHolidayEvent(EmployeeId,  Amount, Body, CreatedAt, Version);
    
// ReSharper disable once ClassNeverInstantiated.Global
public sealed record PaidHolidayUsedEvent(EmployeeId EmployeeId, decimal Amount, EventBody? Body, DateTimeOffset CreatedAt, DomainVersion Version)
    : PaidHolidayEvent(EmployeeId,  Amount, Body, CreatedAt, Version);