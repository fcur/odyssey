namespace Odyssey.Domain;

public sealed class SingleUserYamlConfig
{
    public UserYamlConfig User { get; set; } = null!;
    public string StartDate { get; set; } = null!;
    // public TimeOffYamlConfig TimeOffSettings { get; set; } = null!;
    public LeaveSettingsYamlConfig[] LeaveSettings { get; set; } = null!;
    public TimeOffRequestYamlConfig[] TimeOffRequests { get; set; } = null!;
}

// public sealed class TimeOffYamlConfig
// {
//     public string Paid { get; set; } = null!;
//     public string Unpaid { get; set; } = null!;
//     public string Family { get; set; } = null!;
//     public string Rounding { get; set; } = null!;
// }

