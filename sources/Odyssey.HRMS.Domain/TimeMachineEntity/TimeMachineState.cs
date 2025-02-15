using Odyssey.HRMS.Domain.EmployeeEntity;

namespace Odyssey.HRMS.Domain.TimeMachineEntity;

public sealed record TimeMachineState(DateTimeOffset AtTime, IReadOnlyDictionary<LeaveType, TimespanPair> AggregatedTime)
{
    public TimespanPair this[LeaveType key] => AggregatedTime[key];
}