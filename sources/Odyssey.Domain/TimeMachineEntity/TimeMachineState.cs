using Odyssey.Domain.EmployeeEntity;

namespace Odyssey.Domain.TimeMachineEntity;

public sealed record TimeMachineState(DateTimeOffset AtTime, IReadOnlyDictionary<LeaveType, TimespanPair> AggregatedTime)
{
    public TimespanPair this[LeaveType key] => AggregatedTime[key];
}