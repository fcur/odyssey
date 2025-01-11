using CSharpFunctionalExtensions;

namespace Odyssey.Calendar;

public sealed class CalendarActor
{
    public User User { get; }
    public StartDate StartDate { get; }
    public TimeOffSettings TimeOffSettings { get; }

    public CalendarActor(User user, StartDate startDate, TimeOffSettings timeOffSettings)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(startDate);
        ArgumentNullException.ThrowIfNull(timeOffSettings);
        
        User = user;
        StartDate = startDate;
        TimeOffSettings = timeOffSettings;
    }

    public static Result<CalendarActor> Crete(User user, StartDate startDate, TimeOffSettings timeOffSettings)
    {
        var validationResult = Validate(user, startDate, timeOffSettings);

        if (validationResult.IsFailure)
        {
            return Result.Failure<CalendarActor>(
                $"Failed to create {nameof(CalendarActor)} instance due to error: '{validationResult.Error}'.");
        }

        return new CalendarActor(user, startDate, timeOffSettings);
    }

    public Result<IReadOnlyCollection<ITimeOffAdditionEvent>, Error> GetTimeOffAdditionEvents(DateTimeOffset atTime)
    {
        var paidTimeOffAdditionEvents = PreparePaidTimeOffAdditionEvents(atTime);

        var unpaidTimeOffAdditionEvents = Array.Empty<ITimeOffAdditionEvent>();
        var familyTimeOffAdditionEvents = Array.Empty<ITimeOffAdditionEvent>();

        return paidTimeOffAdditionEvents;
    }

    private Result<IReadOnlyCollection<ITimeOffAdditionEvent>, Error> PreparePaidTimeOffAdditionEvents(DateTimeOffset atTime)
    {
        var monthlyCheckpoints = CalendarTools.BuildMonthlyCheckpoints(StartDate.Value, atTime);
        var timeAccruals = CalendarTools
            .PrepareMonthlyTimeAccruals(monthlyCheckpoints, TimeOffSettings.Paid.Value)
            .ToArray();

        return timeAccruals.Select(PaidTimeOffAdditionEvent.Create).ToArray();
    }

    private static Result Validate(User? user, StartDate? startDate, TimeOffSettings? timeOffSettings)
    {
        if (user is null)
        {
            return Result.Failure<CalendarActor>($"{nameof(User)} cannot be undefined.");
        }

        if (startDate is null)
        {
            return Result.Failure<CalendarActor>($"{nameof(StartDate)} cannot be undefined.");
        }

        if (timeOffSettings is null)
        {
            return Result.Failure<CalendarActor>($"{nameof(TimeOffSettings)} cannot be undefined.");
        }

        return Result.Success();
    }
}

public sealed record StartDate(DateTimeOffset Value)
{
    public static StartDate Parse(string data)
    {
        var dateTime = DateTimeOffset.Parse(data);
        return new StartDate(dateTime);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}