namespace Odyssey.HRMS.Domain.TimeMachine;

public sealed record TimespanPair(TimeSpan Accrued, TimeSpan Used)
{
    public TimeSpan Delta => Accrued - Used;
}