namespace Odyssey.Domain.TimeMachineEntity;

public sealed record TimeMachineError(string Type, string Message) : Error(Type, Message)
{
    public static TimeMachineError InvalidStartDate(DateTimeOffset startDate) =>
        new TimeMachineError("InvalidStartDate", $"Employee start date '{startDate}' should be less than current time");
}