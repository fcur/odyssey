namespace Odyssey.Domain.EmployeeEntity;

public readonly record struct EmployeeId(Guid Value)
{
    public static readonly EmployeeId Empty = new EmployeeId(Guid.Empty);

    public static EmployeeId Parse(string id)
    {
        return new EmployeeId(Guid.Parse(id));
    }

    public static EmployeeId New()
    {
        var timeNow = DateTimeOffset.UtcNow;
        var id = Guid.CreateVersion7(timeNow);
        var employeeId = new EmployeeId(id);

        return employeeId;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Value.ToString("D");
    }
}