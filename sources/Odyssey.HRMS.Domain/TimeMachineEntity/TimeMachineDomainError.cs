using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.TimeMachineEntity;

public sealed record TimeMachineDomainError(string Type, string Message) : DomainError(Type, Message)
{
    public static TimeMachineDomainError InvalidStartDate(DateTimeOffset startDate) =>
        new TimeMachineDomainError("InvalidStartDate", $"Employee start date '{startDate}' should be less than current time");
}