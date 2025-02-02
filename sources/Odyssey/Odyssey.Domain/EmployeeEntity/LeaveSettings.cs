namespace Odyssey.Domain.EmployeeEntity;

public sealed record LeaveSettings(LeaveType Type, LeaveDetails? Details)
{
    public static IReadOnlyCollection<LeaveSettings> CreateDefault()
    {
        var paidTimeOffAccrual = new LeaveAccrual(TimeSpan.FromDays(20), AccrualPeriod.Yearly, AccrualInterval.Monthly);
        var paidTimeOffDetails = new RecurringLeaveAccrualDetails(paidTimeOffAccrual, LeaveLimits.None);

        var unpaidTimeOffAccrual = new LeaveAccrual(TimeSpan.FromDays(20), AccrualPeriod.Yearly, AccrualInterval.Monthly);
        var unpaidTimeOffDetails = new RecurringLeaveAccrualDetails(unpaidTimeOffAccrual, LeaveLimits.None);

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

    public static LeaveSettings CreatePaidTimeOff(LeaveAccrual accrual, LeaveLimits? limits)
    {
        var paidTimeOffDetails = new RecurringLeaveAccrualDetails(accrual, limits);

        return new LeaveSettings(LeaveType.PaidTimeOff, paidTimeOffDetails);
    }
}
public abstract record LeaveDetails { }

public record BankHolidaySettings(params DateTimeOffset[] Days) : LeaveDetails;

public record RecurringLeaveAccrualDetails(LeaveAccrual Accrual, LeaveLimits? Limits) : LeaveDetails;