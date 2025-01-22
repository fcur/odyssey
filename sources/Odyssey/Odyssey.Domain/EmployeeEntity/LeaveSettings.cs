namespace Odyssey.Domain.EmployeeEntity;

public sealed record LeaveSettings(LeaveType Type, ILeaveDetails? Details)
{
    public static IReadOnlyCollection<LeaveSettings> CreateDefault()
    {
        var paidTimeOffAccrual = new TimeOffAccrual(TimeSpan.FromDays(20), TimeOffAccrualPolicy.Yearly);
        var paidTimeOffDetails = new RecurringTimeOffSettings(paidTimeOffAccrual, TimeOffLimits.None);

        var unpaidTimeOffAccrual = new TimeOffAccrual(TimeSpan.FromDays(20), TimeOffAccrualPolicy.Yearly);
        var unpaidTimeOffDetails = new RecurringTimeOffSettings(unpaidTimeOffAccrual, TimeOffLimits.None);

        var bankHolidayDetails = new BankHolidaySettings(DateTimeOffset.Parse("2025-01-01"));

        var customLeaveType1 = new LeaveType("Custom 1", "custom-1", null);

        return
        [
            new LeaveSettings(LeaveType.PaidTimeOff, paidTimeOffDetails),
            new LeaveSettings(LeaveType.UnpaidTimeOff, unpaidTimeOffDetails),
            new LeaveSettings(LeaveType.SickLeave, null),
            new LeaveSettings(LeaveType.BankHoliday, bankHolidayDetails),
            new LeaveSettings(customLeaveType1, null),
        ];
    }

    public static LeaveSettings CreatePaidTimeOff(TimeOffAccrual accrual, TimeOffLimits? limits)
    {
        var paidTimeOffDetails = new RecurringTimeOffSettings(accrual, limits);

        return new LeaveSettings(LeaveType.PaidTimeOff, paidTimeOffDetails);
    }
}

public readonly record struct TimeOffLimitsType(string PolicyName)
{
    private const string MaxValueTypeName = "MAX_VALUE";
    private const string ResetDateTypeName = "RESET_DATE";

    public static readonly TimeOffLimitsType MaxValue = new TimeOffLimitsType(MaxValueTypeName);
    public static readonly TimeOffLimitsType ResetAtDate = new TimeOffLimitsType(ResetDateTypeName);
    public static readonly TimeOffLimitsType Unknown = new TimeOffLimitsType(string.Empty);

    public static TimeOffLimitsType Parse(string value)
    {
        var target = value.ToUpperInvariant();
        var result = target switch
        {
            MaxValueTypeName => MaxValue,
            ResetDateTypeName => ResetAtDate,
            _ => Unknown
        };

        return result;
    }
}

public interface ILeaveDetails { }

public record BankHolidaySettings(params DateTimeOffset[] Days) : ILeaveDetails;

public record RecurringTimeOffSettings(TimeOffAccrual Accrual, TimeOffLimits? Limits) : ILeaveDetails;