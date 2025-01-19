namespace Odyssey.Calendar;

public readonly record struct TimeOffType(string TypeName)
{
    private const string PaidTypeName = "PAID";
    private const string UnpaidTypeName = "UNPAID";
    
    public static readonly TimeOffType Paid = new TimeOffType(PaidTypeName);
    public static readonly TimeOffType Unpaid = new TimeOffType(UnpaidTypeName);
    public static readonly TimeOffType Unknown = new TimeOffType(string.Empty);

    public static TimeOffType Parse(string value)
    {
        var target = value.ToUpperInvariant();
        
        var result = target switch
        {
            PaidTypeName => Paid,
            UnpaidTypeName => Unpaid,
            _ => Unknown
        };
        
        return result;
    }
    
}