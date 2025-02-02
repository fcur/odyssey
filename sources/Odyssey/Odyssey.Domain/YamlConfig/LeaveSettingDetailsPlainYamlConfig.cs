namespace Odyssey.Domain.YamlConfig;

public class LeaveSettingDetailsPlainYamlConfig
{
    public string? Duration { get; set; }
    public string? Period { get; set; }
    public string? Interval { get; set; }
    public string[]? Days { get; set; }
}

public sealed class RecurringTimeOffSettingsPlainYamlConfig : LeaveSettingDetailsPlainYamlConfig
{
    public new required string Duration { get; set; }
    public new required string Period { get; set; }
    public new required string Interval { get; set; }
}

public sealed class BankHolidaySettingsPlainYamlConfig : LeaveSettingDetailsPlainYamlConfig
{
    public new required string[] Days { get; set; }
}