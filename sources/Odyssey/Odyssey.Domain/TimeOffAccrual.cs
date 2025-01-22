using Odyssey.Domain.EmployeeEntity;

namespace Odyssey.Domain;

public readonly record struct TimeOffAccrual(TimeSpan Value, TimeOffAccrualPolicy Policy)
{
    public static TimeOffAccrual Parse(string duration, string policy)
    {
        return new TimeOffAccrual(TimeSpan.Parse(duration), TimeOffAccrualPolicy.Parse(policy));
    }
}