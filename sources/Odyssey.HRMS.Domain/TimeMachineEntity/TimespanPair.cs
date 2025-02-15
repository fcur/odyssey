namespace Odyssey.HRMS.Domain.TimeMachineEntity;

public sealed record TimespanPair(TimeSpan Accrued, TimeSpan Used)
{
    public TimeSpan Delta => Accrued - Used;
}