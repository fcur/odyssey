using Odyssey.HRMS.Domain.YamlConfig;

namespace Odyssey.HRMS.Domain;

public sealed class LeaveSettingsYamlConfig
{
    public string Type { get; set; } = null!;
    public LeaveSettingDetailsPlainYamlConfig? Details { get; set; } = null;
}