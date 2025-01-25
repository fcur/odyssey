using CSharpFunctionalExtensions;
using Odyssey.Domain.EmployeeEntity;

namespace Odyssey.Domain.TimeMachineEntity;

public sealed class TimeMachine
{
    private readonly Employee _actor;
    private readonly IReadOnlyCollection<TimeOffRequest> _timeOffRequests;

    private TimeMachine(Employee actor, IReadOnlyCollection<TimeOffRequest> timeOffRequests)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(timeOffRequests);

        _actor = actor;
        _timeOffRequests = timeOffRequests;
    }

    public static Result<TimeMachine, Error> Create(Employee employee,
        IReadOnlyCollection<TimeOffRequest> timeOffRequests)
    {
        var now = DateTimeOffset.UtcNow;

        if (employee.StartDate >= now)
        {
            return Result.Failure<TimeMachine, Error>(TimeMachineError.InvalidStartDate(employee.StartDate.Value));
        }

        return new TimeMachine(employee, timeOffRequests);
    }

    public Result<TimeMachineState, Error> GoTo(DateTimeOffset atTime)
    {
        var leaveSettings = _actor.LeaveSettings;
        var startDate = _actor.StartDate.Value;

        var timeAccruals = leaveSettings.Select(v => BuildTimeAccruals(v, startDate, atTime))
            .ExceptNull()
            .ToDictionary(v => v!.Type, v => v!.Duration);

        var timeRequests = _timeOffRequests?.GroupBy(v => v.LeaveType)
            .ToDictionary(v => v.Key, v => Aggregate(v.ToArray())) ?? new Dictionary<LeaveType, TimeSpan>();

        var keys = timeAccruals.Keys.Concat(timeRequests.Keys).Distinct().ToArray();

        var results = keys.ToDictionary(v => v,
            v =>
            {
                var accrued = timeAccruals.TryGetValue(v, out var accruedResult) ? accruedResult : TimeSpan.Zero;
                var used = timeRequests.TryGetValue(v, out var usedResult) ? usedResult : TimeSpan.Zero;
                return new TimespanPair(accrued, used);
            });

        return new TimeMachineState(atTime, results);
    }

    public static TimeAccrual[] BuildRecurringTimeOffAccruals(
        RecurringLeaveAccrualDetails rts,
        DateTimeOffset startDate,
        DateTimeOffset atTime)
    {
        var (value, period, interval) = rts.Accrual;

        if (period != AccrualPeriod.Yearly)
        {
            throw new NotImplementedException($"Not implemented for period: '{period.Value}'");
        }

        var timeAccruals = interval.Value switch
        {
            AccrualInterval.YearlyTypeName => HandleYearlyAccruals(value, startDate, atTime),
            AccrualInterval.MonthlyTypeName => HandleMonthlyAccruals(value, startDate, atTime),
            _ => throw new NotImplementedException($"Not implemented for interval: '{interval.Value}'")
        };

        return timeAccruals;
    }

    private static TimeAccrual[] HandleMonthlyAccruals(TimeSpan value, DateTimeOffset startDate, DateTimeOffset atTime)
    {
        var monthlyCheckpoints = CalendarTools.BuildMonthlyCheckpoints(startDate, atTime);
        var timeAccruals = CalendarTools
            .PrepareMonthlyTimeAccruals(monthlyCheckpoints, value)
            .ToArray();

        return timeAccruals;
    }

    private static TimeAccrual[] HandleYearlyAccruals(TimeSpan value, DateTimeOffset startDate, DateTimeOffset atTime)
    {
        var yearlyCheckpoints = CalendarTools.BuildYearlyCheckpoints(startDate, atTime);
        var timeAccruals = CalendarTools
            .PrepareYearlyTimeAccruals(yearlyCheckpoints, value)
            .ToArray();

        return timeAccruals;
    }

    public static TimeOffAccrualResult? BuildTimeAccruals(LeaveSettings settings, DateTimeOffset startDate, DateTimeOffset atTime)
    {
        var timeOffAccrual = settings.Type.Code switch
        {
            LeaveType.PaidTimeOffCode or LeaveType.UnpaidTimeOffCode when settings.Details is RecurringLeaveAccrualDetails rts =>
                new TimeOffAccrualResult(settings.Type, AggregateRecurringTimeOffAccruals(rts, startDate, atTime)),
            _ => null
        };

        return timeOffAccrual;
    }
    
    private static TimeSpan AggregateRecurringTimeOffAccruals(RecurringLeaveAccrualDetails rts, DateTimeOffset startDate, DateTimeOffset atTime)
    {
        var accruals = BuildRecurringTimeOffAccruals(rts, startDate, atTime);
        return Aggregate(accruals);
    }

    private static TimeSpan Aggregate(IReadOnlyCollection<TimeAccrual> timeAccruals)
    {
        var ticks = timeAccruals.Aggregate(0D,
            (current, timeAccrual) => current + timeAccrual.Ticks);

        var roundedTicks = Math.Round(ticks);

        return TimeSpan.FromTicks((long)roundedTicks);
    }

    private static TimeSpan Aggregate(IReadOnlyCollection<TimeOffRequest> timeOffRequests)
    {
        return timeOffRequests.Aggregate(TimeSpan.Zero,
            (current, timeOffRequest) => current.Add(timeOffRequest.TimeOffSettings.Duration));
    }
}