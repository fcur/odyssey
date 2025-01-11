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
    public string Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
}

public sealed class TimeOffYamlConfig
{
    public string Paid { get; set; }
    public string Unpaid { get; set; }
    public string Family { get; set; }
    public string Rounding { get; set; }
}

public sealed class TimeOffRequestYamlConfig
{
    public string Type { get; set; }
    public string Start { get; set; }
    public string Finish { get; set; }
}