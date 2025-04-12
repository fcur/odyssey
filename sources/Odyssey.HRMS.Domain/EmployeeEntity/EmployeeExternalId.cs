namespace Odyssey.HRMS.Domain.EmployeeEntity;

public sealed record EmployeeExternalId(string Value)
{
    public static readonly EmployeeExternalId? Unset = null;
}