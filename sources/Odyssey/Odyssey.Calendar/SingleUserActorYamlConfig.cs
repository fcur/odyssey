namespace Odyssey.Calendar;

public sealed class SingleUserActorYamlConfig
{
    public UserYamlConfig User { get; set; }
    public string StartDate { get; set; }
    public TimeOffYamlConfig TimeOffSettings { get; set; }
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

public static class SingleUserConfigExtensions
{
    public static CalendarActor ToCalendarActor(this SingleUserActorYamlConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrEmpty(config.StartDate);
        ArgumentNullException.ThrowIfNull(config.User);
        ArgumentException.ThrowIfNullOrEmpty(config.User.Id);
        ArgumentException.ThrowIfNullOrEmpty(config.User.Email);
        ArgumentException.ThrowIfNullOrEmpty(config.User.Name);
        ArgumentNullException.ThrowIfNull(config.TimeOffSettings);
        ArgumentException.ThrowIfNullOrEmpty(config.TimeOffSettings.Paid);
        ArgumentException.ThrowIfNullOrEmpty(config.TimeOffSettings.Unpaid);
        ArgumentException.ThrowIfNullOrEmpty(config.TimeOffSettings.Family);
        ArgumentException.ThrowIfNullOrEmpty(config.TimeOffSettings.Rounding);
        // TBD: additional validations 

        var userId = UserId.Parse(config.User.Id);
        var email = new Email(config.User.Email);
        var userName = UserName.Parse(config.User.Name);

        var paidTimeOffDuration = PaidTimeOffDuration.Parse(config.TimeOffSettings.Paid);
        var unpaidTimeOffDuration = UnpaidTimeOffDuration.Parse(config.TimeOffSettings.Unpaid);
        var familyTimeOffDuration = FamilyTimeOffDuration.Parse(config.TimeOffSettings.Family);
        var timeOffRounding = TimeOffRounding.Parse(config.TimeOffSettings.Rounding);

        var user = new User(userName, email, userId);
        var startDate = StartDate.Parse(config.StartDate);
        var timeOffSettings = new TimeOffSettings(
            paidTimeOffDuration,
            unpaidTimeOffDuration,
            familyTimeOffDuration,
            timeOffRounding);

        var calendarActor = CalendarActor.Crete(user, startDate, timeOffSettings);

        return calendarActor;
    }
}