using Odyssey.Domain.YamlConfig;

namespace Odyssey.Domain;

public sealed class LeaveSettingsYamlConfig
{
    public string Type { get; set; } = null!;
    public LeaveSettingDetailsPlainYamlConfig? Details { get; set; } = null;
}