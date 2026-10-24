using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.UserEntity;

namespace Odyssey.HRMS.Domain.EmployeeEntity;

// TBD: initial leave counters
public sealed record Employee(
    EmployeeId Id,
    User User,
    StartDate StartDate,
    EmployeeExternalId? ExternalId,
    IReadOnlyCollection<LeaveSettings> LeaveSettings,
    DateTimeOffset ChangedAt,
    DomainVersion Version,
    ulong RowVersion)
    : DomainEntity<EmployeeId, EmployeeChangedEvent>(Id, ChangedAt, Version)
{
    public static Result<Employee> Create(
        EmployeeId id,
        User user,
        StartDate startDate,
        IReadOnlyCollection<LeaveSettings> leaveSettings)
    {
        var validationResult = Validate(id, user, startDate, leaveSettings);

        if (validationResult.IsFailure)
        {
            return Result.Failure<Employee>(
                $"Failed to create {nameof(Employee)} instance due to error: '{validationResult.Error}'.");
        }

        var changedAt = DateTimeOffset.UtcNow;
        var version = DomainVersion.New;
        var rowVersion = 0UL;
        var externalId = EmployeeExternalId.Unset;

        var @event = new EmployeeChangedEvent(id, changedAt, version);
        var employee = new Employee(id, user, startDate, externalId, leaveSettings, changedAt, version, rowVersion);
        employee.EnqueueEvent(@event);

        return employee;
    }

    private static Result Validate(EmployeeId? id,
        User? user,
        StartDate? startDate,
        IReadOnlyCollection<LeaveSettings>? leaveSettings)
    {
        if (id is null)
        {
            return Result.Failure<Employee>($"{nameof(EmployeeId)} cannot be undefined.");
        }

        if (user is null)
        {
            return Result.Failure<Employee>($"{nameof(User)} cannot be undefined.");
        }

        if (startDate is null)
        {
            return Result.Failure<Employee>($"{nameof(StartDate)} cannot be undefined.");
        }

        if (leaveSettings is null)
        {
            return Result.Failure<Employee>($"{nameof(LeaveSettings)} collection cannot be undefined.");
        }

        return Result.Success();
    }
}