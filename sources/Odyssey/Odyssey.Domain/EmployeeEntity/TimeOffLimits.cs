using Odyssey.Domain.EmployeeEntity;

namespace Odyssey.Domain;

public sealed record TimeOffLimits(string Name, TimeOffLimitsType Type)
{
    public static TimeOffLimits? None = null;
}