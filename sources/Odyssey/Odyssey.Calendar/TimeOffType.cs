namespace Odyssey.Calendar;

public readonly record struct TimeOffType(string TypeName)
{
    private const string PaidTypeName = "PAID";
    private const string UnpaidTypeName = "UNPAID";
    private const string FamilyTypeName = "FAMILY";
    
    public static readonly TimeOffType Paid = new TimeOffType(PaidTypeName);
    public static readonly TimeOffType Unpaid = new TimeOffType(UnpaidTypeName);
    public static readonly TimeOffType Family = new TimeOffType(FamilyTypeName);
    public static readonly TimeOffType Unknown = new TimeOffType(string.Empty);

    public static TimeOffType Parse(string value)
    {
        var target = value.ToUpperInvariant();
        
        var result = target switch
        {
            PaidTypeName => Paid,
            UnpaidTypeName => Unpaid,
            FamilyTypeName => Family,
            _ => Unknown
        };
        
        return result;
    }
    
}