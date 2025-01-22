namespace Odyssey.Domain.EmployeeEntity;

public sealed record LeaveType(string Name, string Code, string? Description)
{
    public const string PaidTimeOffCode = "PAID-TIME-OFF";
    public const string UnpaidTimeOffCode = "UNPAID-TIME-OFF";
    public const string FamilyLeaveCode = "FAMILY-LEAVE";
    public const string MilitaryLeaveCode = "MILITARY-LEAVE";
    public const string SickLeaveCode = "SICK-LEAVE";
    public const string MarriageLeaveCode = "MARRIAGE-LEAVE";
    public const string ParentalLeaveCode = "PARENTAL-LEAVE";
    public const string BankHolidayCode = "BANK-HOLIDAY";

    public static readonly LeaveType PaidTimeOff = new LeaveType("Paid time off", PaidTimeOffCode, null);
    public static readonly LeaveType UnpaidTimeOff = new LeaveType("Unpaid time off", UnpaidTimeOffCode, null);
    public static readonly LeaveType FamilyLeave = new LeaveType("Family leave", FamilyLeaveCode, null);
    public static readonly LeaveType MilitaryLeave = new LeaveType("Military leave", MilitaryLeaveCode, null);
    public static readonly LeaveType SickLeave = new LeaveType("Sick leave", SickLeaveCode, null);
    public static readonly LeaveType MarriageLeave = new LeaveType("Marriage leave", MarriageLeaveCode, null);
    public static readonly LeaveType ParentalLeave = new LeaveType("Parental leave", ParentalLeaveCode, null);
    public static readonly LeaveType BankHoliday = new LeaveType("Bank holiday", BankHolidayCode, null);
    public static readonly LeaveType Unknown = new LeaveType(string.Empty, string.Empty, null);

    public static LeaveType Parse(string value)
    {
        var target = value.ToUpperInvariant();

        var result = target switch
        {
            PaidTimeOffCode => PaidTimeOff,
            UnpaidTimeOffCode => UnpaidTimeOff,
            FamilyLeaveCode => FamilyLeave,
            MilitaryLeaveCode => MilitaryLeave,
            SickLeaveCode => SickLeave,
            MarriageLeaveCode => MarriageLeave,
            ParentalLeaveCode => ParentalLeave,
            BankHolidayCode => BankHoliday,
            _ => Unknown
        };

        return result;
    }
    
    public override int GetHashCode()
    {
        return Code.GetHashCode();
    }
}