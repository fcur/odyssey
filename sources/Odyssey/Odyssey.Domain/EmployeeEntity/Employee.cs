using CSharpFunctionalExtensions;
using Odyssey.Domain.UserEntity;

namespace Odyssey.Domain.EmployeeEntity;

public sealed class Employee
{
    public EmployeeId Id { get; }
    public User User { get; }

    public StartDate StartDate { get; }

    // TBD: initial leave counters
    public IReadOnlyCollection<LeaveSettings> LeaveSettings { get; }

    public Employee(EmployeeId id, User user, StartDate startDate, IReadOnlyCollection<LeaveSettings> leaveSettings)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(startDate);
        ArgumentNullException.ThrowIfNull(leaveSettings);

        Id = id;
        User = user;
        StartDate = startDate;
        LeaveSettings = leaveSettings;
    }

    public static Result<Employee> Create(EmployeeId id,
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

        return new Employee(id, user, startDate, leaveSettings);
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