namespace Odyssey.HRMS.Domain;

public sealed class TimeOffRequestYamlConfig
{
    public string Type { get; set; } = null!;
    public string Start { get; set; } = null!;
    public string Finish { get; set; } = null!;
}