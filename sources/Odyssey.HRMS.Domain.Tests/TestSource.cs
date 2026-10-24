using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.Tests;

public static class TestSource
{
    #region activity names
    public const string TeamImportActivityName = "TeamImport";
    public const string PaidHolidayAccrualActivityName = "PaidHolidayAccrual";
    public const string NotifyEmployeeActivityName = "NotifyEmployee";
    #endregion

    #region event names
    public const string EmployeeAddedEventName = "TeamEmployeeAdded";
    public const string TeamImportFailedEventName = "TeamImportFailed";
    public const string PaidHolidayAccruedEventName = "PaidHolidayAccrued";
    public const string PaidHolidayAccrualFailedEventName = "PaidHolidayAccrualFailed";
    public const string NotificationSentEventName = "NotificationSent";
    public const string NotificationFailedEventName = "NotificationNotSent";
    #endregion
    
    #region dependency keys
    public const string TeamIdDependencyKey = "TeamId";
    public const string EmployeeIdDependencyKey = "EmployeeId";
    public const string BalanceDependencyKey = "Balance";
    public const string AmountAddedDependencyKey = "AmountAdded";
    #endregion
    
    #region result keys
    public const string EmployeeIdResultKey = "EmployeeId";
    public const string TeamIdResultKey = "TeamId";
    public const string BalanceResultKey = "Balance";
    public const string AmountAddedResultKey = "AmountAdded";
    public const string AtTimeResultKey = "AtTime";
    #endregion

    #region domain versions
    public static DomainVersion Version1 = new DomainVersion(1L);
    public static DomainVersion Version2 = new DomainVersion(2L);
    public static DomainVersion Version3 = new DomainVersion(3L);
    public static DomainVersion Version4 = new DomainVersion(4L);
    public static DomainVersion Version5 = new DomainVersion(5L);
    public static DomainVersion Version6 = new DomainVersion(6L);
    public static DomainVersion Version7 = new DomainVersion(7L);
    public static DomainVersion Version8 = new DomainVersion(8L);
    public static DomainVersion Version9 = new DomainVersion(9L);
    #endregion
}