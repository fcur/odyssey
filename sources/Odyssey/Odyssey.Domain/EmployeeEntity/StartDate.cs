namespace Odyssey.Domain.EmployeeEntity;

public sealed record StartDate(DateTimeOffset Value)
{
    public static StartDate Parse(string data)
    {
        var dateTime = DateTimeOffset.Parse(data);
        return new StartDate(dateTime);
    }

    public static bool operator >=(StartDate? left, DateTimeOffset? right)
    {
        if (left == null || right == null)
        {
            return false;
        }

        return left.Value >= right;
    }

    public static bool operator <=(StartDate? left, DateTimeOffset? right)
    {
        if (left == null || right == null)
        {
            return false;
        }

        return left.Value <= right;
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}