namespace Odyssey.Calendar;

public sealed class SingleUserYamlConfig
{
    public UserYamlConfig User { get; set; }
    public string StartDate { get; set; }
    public TimeOffYamlConfig TimeOffSettings { get; set; }
    public TimeOffRequestYamlConfig[] TimeOffRequests { get; set; }
}

public sealed class UserYamlConfig
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public sealed class TimeOffYamlConfig
{
    public string Paid { get; set; } = null!;
    public string Unpaid { get; set; } = null!;
    public string Family { get; set; } = null!;
    public string Rounding { get; set; } = null!;
}

public sealed class TimeOffRequestYamlConfig
{
    public string Type { get; set; } = null!;
    public string Start { get; set; } = null!;
    public string Finish { get; set; } = null!;
}

public sealed class LeaveSettingsYamlConfig
{
    public string Type { get; set; } = null!;
    public ILeaveSettingDetailsYamlConfig? Details { get; set; }
}

public interface ILeaveSettingDetailsYamlConfig
{
}

public sealed class RecurringTimeOffSettingsYamlConfig : ILeaveSettingDetailsYamlConfig
{
    public string Duration { get; set; } = null!;
    public string Policy { get; set; } = null!;
}

public sealed class BankHolidaySettingsYamlConfig : ILeaveSettingDetailsYamlConfig
{
    public string[] Days { get; set; } = null!;
}