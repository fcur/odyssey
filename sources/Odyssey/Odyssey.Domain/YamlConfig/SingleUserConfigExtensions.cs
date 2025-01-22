using Odyssey.Domain.EmployeeEntity;
using Odyssey.Domain.UserEntity;
using Odyssey.Domain.YamlConfig;

namespace Odyssey.Domain;

public static class SingleUserConfigExtensions
{
    public static (Employee actor, TimeOffRequest[] timeOffRequests) ToDomain(this SingleUserYamlConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrEmpty(config.StartDate);
        ArgumentNullException.ThrowIfNull(config.User);
        ArgumentException.ThrowIfNullOrEmpty(config.User.Email);
        ArgumentException.ThrowIfNullOrEmpty(config.User.Name);
        ArgumentNullException.ThrowIfNull(config.LeaveSettings);

        // TBD: additional validations 

        var email = new Email(config.User.Email);
        var userName = UserName.Parse(config.User.Name);
        var user = new User(UserId.Empty, userName, email);
        var startDate = StartDate.Parse(config.StartDate);
        var leaveSettings = config.LeaveSettings.Select(ToDomain).Where(v=>v.Type!= LeaveType.Unknown).ToArray();

        var employee = Employee.Create(EmployeeId.Empty, user, startDate, leaveSettings).GetValueOrDefault();
        var timeOffRequests = config.TimeOffRequests.Select(ToTimeOffRequestDomain).ToArray();

        return (employee, timeOffRequests);
    }

    public static SingleUserYamlConfig ToConfig(this (Employee Actor, TimeOffRequest[] timeOffRequests) domain)
    {
        var actor = domain.Actor;
        var timeOffRequests = domain.timeOffRequests;

        return new SingleUserYamlConfig
        {
            User = new UserYamlConfig
            {
                Name = string.Join(' ', actor.User.Name.Values),
                Email = actor.User.Email.Value
            },
            StartDate = actor.StartDate.Value.ToString(),
            LeaveSettings = domain.Actor.LeaveSettings.Select(ToConfig).ToArray(),
            TimeOffRequests = timeOffRequests.Select(ToTimeOffRequestYamlConfig).ToArray()
        };
    }

    public static LeaveSettingsYamlConfig ToConfig(this LeaveSettings domain)
    {
        var details = domain.Type.Code switch
        {
            LeaveType.PaidTimeOffCode or LeaveType.UnpaidTimeOffCode when domain.Details is RecurringTimeOffSettings rts
                => rts.ToConfig(),
            LeaveType.BankHolidayCode when domain.Details is BankHolidaySettings bhs => bhs.ToConfig(),
            _ => null
        };

        var result = new LeaveSettingsYamlConfig
        {
            Type = domain.Type.Code,
            Details = details
        };

        return result;
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
        var leaveType = LeaveType.Parse(config.Type);

        var timeOffRequest = new TimeOffRequest(timeOffRequestSettings.Value, createdAt, timeOffType, leaveType);

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

    private static LeaveSettingDetailsPlainYamlConfig ToConfig(this RecurringTimeOffSettings domain)
    {
        var result = new RecurringTimeOffSettingsPlainYamlConfig
        {
            Duration = domain.Accrual.Value.ToString(),
            Policy = domain.Accrual.Policy.Name.ToLowerInvariant()
        };

        return result;
    }

    private static LeaveSettingDetailsPlainYamlConfig ToConfig(this BankHolidaySettings domain)
    {
        var result = new BankHolidaySettingsPlainYamlConfig
        {
            Days = domain.Days.Select(v => v.ToString("yyyy-MM-dd")).ToArray(),
        };

        return result;
    }

    private static ILeaveDetails ToRecurringTimeOffSettings(this LeaveSettingDetailsPlainYamlConfig config)
    {
        var accrual =  TimeOffAccrual.Parse(config.Duration!, config.Policy!);
        var result = new RecurringTimeOffSettings(accrual, TimeOffLimits.None);

        return result;
    }
    
    private static ILeaveDetails ToBankHolidaySettings(this LeaveSettingDetailsPlainYamlConfig config)
    {
        var days = config.Days!.Select(DateTimeOffset.Parse).ToArray();
        var result = new BankHolidaySettings(days);
        
        return result;
    }
    
    private static LeaveSettings ToDomain(this LeaveSettingsYamlConfig config)
    {
        var leaveType = LeaveType.Parse(config.Type);
        
        var details = leaveType.Code switch
        {
            LeaveType.PaidTimeOffCode or LeaveType.UnpaidTimeOffCode  => config.Details!.ToRecurringTimeOffSettings(),
            LeaveType.BankHolidayCode => config.Details!.ToBankHolidaySettings(),
            _ => null
        };

        var result = new LeaveSettings(leaveType, details);

        return result;
    }
}