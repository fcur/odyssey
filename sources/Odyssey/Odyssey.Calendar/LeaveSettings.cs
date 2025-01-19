namespace Odyssey.Calendar;

public sealed record LeaveSettings(LeaveType Type, ILeaveDetails? Details)
{
    public static IReadOnlyCollection<LeaveSettings> CreateDefault()
    {
        var paidTimeOffAccrual = new TimeOffAccrual(TimeSpan.FromDays(20), TimeOffAccrualPolicy.Yearly);
        var paidTimeOffDetails = new RecurringTimeOffSettings(paidTimeOffAccrual, null);

        var unpaidTimeOffAccrual = new TimeOffAccrual(TimeSpan.FromDays(20), TimeOffAccrualPolicy.Yearly);
        var unpaidTimeOffDetails = new RecurringTimeOffSettings(unpaidTimeOffAccrual, null);

        var bankHolidayDetails = new BankHolidaySettings(DateTimeOffset.Parse("2025-01-01"));

        var customLeaveType1 = new LeaveType("Custom 1", "custom-1", null);
        
        return
        [
            new LeaveSettings(LeaveType.PaidTimeOff, paidTimeOffDetails),
            new LeaveSettings(LeaveType.UnpaidTimeOff, unpaidTimeOffDetails),
            new LeaveSettings(LeaveType.FamilyLeave, null),
            // new LeaveSettings(LeaveType.MilitaryLeave, null),
            // new LeaveSettings(LeaveType.MarriageLeave, null),
            // new LeaveSettings(LeaveType.ParentalLeave, null),
            new LeaveSettings(LeaveType.BankHoliday, bankHolidayDetails),
            new LeaveSettings(customLeaveType1, null),
        ];
    }
}


public interface ILeaveDetails
{
}

public sealed record TimeOffLimits(string Name, TimeOffLimitsType Type);

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

public record BankHolidaySettings(params DateTimeOffset[] Days) : ILeaveDetails;

public record RecurringTimeOffSettings(TimeOffAccrual Accrual, TimeOffLimits? Limits)
    : ILeaveDetails;

public sealed record LeaveType(string Name, string Code, string? Description)
{
    public const string PaidTimeOffCode = "paid-time-off";
    public const string UnpaidTimeOffCode = "unpaid-time-off";
    public const string BankHolidayCode = "bank-holiday";
    
    public static readonly LeaveType PaidTimeOff = new LeaveType("Paid time off", PaidTimeOffCode, null);
    public static readonly LeaveType UnpaidTimeOff = new LeaveType("Unpaid time off", UnpaidTimeOffCode, null);
    public static readonly LeaveType FamilyLeave = new LeaveType("Family leave", "family-leave", null);
    public static readonly LeaveType MilitaryLeave = new LeaveType("Military leave", "military-leave", null);
    public static readonly LeaveType SickLeave = new LeaveType("Sick leave", "sick-leave", null);
    public static readonly LeaveType MarriageLeave = new LeaveType("Marriage leave", "marriage-leave", null);
    public static readonly LeaveType ParentalLeave = new LeaveType("Parental leave", "parental-leave", null);
    public static readonly LeaveType BankHoliday = new LeaveType("Bank holiday", BankHolidayCode, null);
}