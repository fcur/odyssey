namespace Odyssey.Domain.YamlConfig;

public class LeaveSettingDetailsPlainYamlConfig
{
    public string? Duration { get; set; }
    public string? Policy { get; set; }
    public string[]? Days { get; set; }
}

public sealed class RecurringTimeOffSettingsPlainYamlConfig : LeaveSettingDetailsPlainYamlConfig
{
    public new required string Duration { get; set; }
    public new required string Policy { get; set; }
}

public sealed class BankHolidaySettingsPlainYamlConfig : LeaveSettingDetailsPlainYamlConfig
{
    public new required string[] Days { get; set; }
}