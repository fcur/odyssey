namespace Odyssey.Calendar;

public static class SingleUserConfigExtensions
{
    public static (CalendarActor actor, TimeOffRequest[] timeOffRequests) ToDomain(this SingleUserYamlConfig config)
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
        var timeOffRequests = config.TimeOffRequests.Select(ToTimeOffRequestDomain).ToArray();

        return (calendarActor.Value, timeOffRequests);
    }

    public static SingleUserYamlConfig ToConfig(this(CalendarActor Actor, TimeOffRequest[] timeOffRequests) domain)
    {
        var actor = domain.Actor;
        var timeOffRequests = domain.timeOffRequests;
        
        return new SingleUserYamlConfig
        {
            User = new UserYamlConfig
            {
                Name = string.Join(' ', actor.User.Name.Values),
                Email = actor.User.Email.Value,
                Id = actor.User.Id.Value.ToString("D")
            },
            StartDate = actor.StartDate.Value.ToString(),
            TimeOffSettings = new TimeOffYamlConfig
            {
                Paid = actor.TimeOffSettings.Paid.Value.ToString(),
                Unpaid = actor.TimeOffSettings.Unpaid.Value.ToString(),
                Family = actor.TimeOffSettings.Family.Value.ToString(),
                Rounding = actor.TimeOffSettings.Rounding.Value.ToString()
            },
            TimeOffRequests = timeOffRequests.Select(ToTimeOffRequestYamlConfig).ToArray()
        };
    }
    
    private static TimeOffRequest ToTimeOffRequestDomain(TimeOffRequestYamlConfig config)
    {
        ArgumentException.ThrowIfNullOrEmpty(config.Start);
        ArgumentException.ThrowIfNullOrEmpty(config.Finish);
        ArgumentException.ThrowIfNullOrEmpty(config.Type);

        var startAt = DateTimeOffset.Parse(config.Start);
        var finishAt = DateTimeOffset.Parse(config.Finish);

        var timeOffRequestSettings = TimeOffRequestSettings.Create(startAt, finishAt);
        var createdAt = timeOffRequestSettings.Value.StartAt.AddDays(-1);
        var timeOffType = TimeOffType.Parse(config.Type);

        var timeOffRequest = new TimeOffRequest(timeOffRequestSettings.Value, createdAt, timeOffType);

        return timeOffRequest;
    }

    private static TimeOffRequestYamlConfig ToTimeOffRequestYamlConfig(TimeOffRequest domain)
    {
        return new TimeOffRequestYamlConfig
        {
            Type = domain.Type.TypeName.ToLowerInvariant(),
            Start = domain.StartAt.ToString(),
            Finish = domain.FinishAt.ToString()
        };
    }
}