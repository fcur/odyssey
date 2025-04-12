using Odyssey.HRMS.Domain.EmployeeEntity;

namespace Odyssey.HRMS.Domain.TimeMachine;

public sealed record TimeMachineState(DateTimeOffset AtTime, IReadOnlyDictionary<LeaveType, TimespanPair> AggregatedTime)
{
    public TimespanPair this[LeaveType key] => AggregatedTime[key];
}