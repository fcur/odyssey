namespace Calendar;

public static class CalendarTools
{
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

    public static IEnumerable<double> PrepareMonthlyTimeAccruals(DateTimeOffset[] checkpoints, TimeSpan perYearDuration)
    {
        for (int i = 0, j = 1; j < checkpoints.Length; i++, j++)
        {
            var periodStart = checkpoints[i];
            var periodEnd = checkpoints[j];

            var duration = periodEnd - periodStart;
            var ticksInYear = DateTime.IsLeapYear(periodStart.Year)
                ? PredefinedPeriods.LeapYearTicks
                : PredefinedPeriods.RegularYearTicks;

            var timeOffTicks = duration.Ticks / ticksInYear * perYearDuration.Ticks;

            yield return timeOffTicks;
        }
    }
}