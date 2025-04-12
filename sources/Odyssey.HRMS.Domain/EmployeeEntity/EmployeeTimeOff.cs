using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Story;

namespace Odyssey.HRMS.Domain.EmployeeEntity;

public sealed class EmployeeTimeOff : AggregateRoot<EmployeeId, EmployeeTimeOffState, HolidayEvent>
{
    private EmployeeTimeOff(EmployeeId id, EmployeeTimeOffState state, IReadOnlyCollection<HolidayEvent> domainEvents)
        : base(id, state, domainEvents) { }
    
    public static EmployeeTimeOff Create(EmployeeId id, IReadOnlyCollection<HolidayEvent> domainEvents)
    {
        var state = EmployeeTimeOffState.Create();
        
        return new EmployeeTimeOff(id, state, domainEvents);
    }
}

public sealed class EmployeeTimeOffState : AggregateRootState<HolidayEvent>
{
    public static EmployeeTimeOffState Create() => new EmployeeTimeOffState();
    protected internal override void Apply(HolidayEvent domainEvent)
    {
        switch (domainEvent)
        {   
            case  PaidHolidayAccruedEvent accruedEvent:
                Apply(accruedEvent);
                break;
            case PaidHolidayUsedEvent usedEvent:
                Apply(usedEvent);
                break;
            default: throw new NotImplementedException();
        }
    }
    
    private void Apply(PaidHolidayAccruedEvent accruedEvent)
    {
        throw new NotImplementedException();
    }
    
    private void Apply(PaidHolidayUsedEvent usedEvent)
    {
        throw new NotImplementedException();
    }
}

public abstract record HolidayEvent(EmployeeId EmployeeId, decimal Amount, StoryEventBody? Body, DateTimeOffset CreatedAt, DomainVersion Version)
    : DomainEvent(CreatedAt, Version);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed record PaidHolidayAccruedEvent(EmployeeId EmployeeId, decimal Amount, StoryEventBody? Body, DateTimeOffset CreatedAt, DomainVersion Version)
    : HolidayEvent(EmployeeId,  Amount, Body, CreatedAt, Version);
    
// ReSharper disable once ClassNeverInstantiated.Global
public sealed record PaidHolidayUsedEvent(EmployeeId EmployeeId, decimal Amount, StoryEventBody? Body, DateTimeOffset CreatedAt, DomainVersion Version)
    : HolidayEvent(EmployeeId,  Amount, Body, CreatedAt, Version);