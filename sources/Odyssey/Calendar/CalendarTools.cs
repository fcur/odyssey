namespace Calendar;

public static class CalendarTools
{
    public static readonly double LeapYearTicks = TimeSpan.FromDays(366).Ticks;
    public static readonly double RegularYearTicks = TimeSpan.FromDays(365).Ticks;
    
    public static DateTimeOffset[] BuildMonthlyCheckpoints(DateTimeOffset startDate, DateTimeOffset atTime)
    {
        var nextMonth = startDate.AddMonths(1);
        var time = new DateTimeOffset(nextMonth.Year, nextMonth.Month, 1, 0, 0, 0, startDate.Offset);

        if (time > atTime)
        {
            time = atTime;
        }

        var checkpoints = new List<DateTimeOffset> { startDate };

        while (time < atTime)
        {
            checkpoints.Add(time);
            time = time.AddMonths(1);
        }

        checkpoints.Add(atTime);

        return checkpoints.ToArray();
    }

    public static IEnumerable<TimeAccrual> PrepareMonthlyTimeAccruals(DateTimeOffset[] checkpoints, TimeSpan perYearDuration)
    {
        for (int i = 0, j = 1; j < checkpoints.Length; i++, j++)
        {
            var periodStart = checkpoints[i];
            var periodEnd = checkpoints[j];

            var duration = periodEnd - periodStart;
            var ticksInYear = DateTime.IsLeapYear(periodStart.Year)
                ? LeapYearTicks
                : RegularYearTicks;

            var timeOffTicks = duration.Ticks / ticksInYear * perYearDuration.Ticks;

            yield return new TimeAccrual(timeOffTicks, periodStart, periodEnd);
        }
    }
}

public sealed record TimeAccrual(double Ticks, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);