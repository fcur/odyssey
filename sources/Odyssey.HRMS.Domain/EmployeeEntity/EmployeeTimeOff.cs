using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Story;

namespace Odyssey.HRMS.Domain.EmployeeEntity;

public sealed class EmployeeTimeOff : AggregateRoot<EmployeeId, EmployeeTimeOffState>
{
    private EmployeeTimeOff(EmployeeId id, EmployeeTimeOffState state, IReadOnlyCollection<DomainEvent> domainEvents)
        : base(id, state, domainEvents) { }
    
    public static EmployeeTimeOff Create(EmployeeId id, IReadOnlyCollection<DomainEvent> domainEvents)
    {
        var state = EmployeeTimeOffState.Create();
        
        return new EmployeeTimeOff(id, state, domainEvents);
    }
}

public sealed class EmployeeTimeOffState : AggregateRootState
{
    public static EmployeeTimeOffState Create() => new EmployeeTimeOffState();
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

public abstract record HolidayEvent(EmployeeId EmployeeId, decimal Amount, EventBody? Body, DateTimeOffset CreatedAt, DomainVersion Version)
    : DomainEvent(CreatedAt, Version);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed record PaidHolidayAccruedEvent(EmployeeId EmployeeId, decimal Amount, EventBody? Body, DateTimeOffset CreatedAt, DomainVersion Version)
    : HolidayEvent(EmployeeId,  Amount, Body, CreatedAt, Version);
    
// ReSharper disable once ClassNeverInstantiated.Global
public sealed record PaidHolidayUsedEvent(EmployeeId EmployeeId, decimal Amount, EventBody? Body, DateTimeOffset CreatedAt, DomainVersion Version)
    : HolidayEvent(EmployeeId,  Amount, Body, CreatedAt, Version);