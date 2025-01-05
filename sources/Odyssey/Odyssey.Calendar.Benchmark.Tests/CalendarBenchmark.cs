using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace Odyssey.Calendar.Benchmark.Tests;

[CPUUsageDiagnoser]
[RankColumn]
[MemoryDiagnoser]
public class CalendarBenchmark
{
    private static readonly TimeSpan PerYearDuration;
    private static readonly DateTimeOffset[] Checkpoints;
    
    static CalendarBenchmark()
    {
        var startDate = DateTimeOffset.Parse("2000-01-01");
        var atTime = startDate.AddYears(1000);
        PerYearDuration = TimeOffSettings.CreateDefault().Paid.Value;
        
        Checkpoints = CalendarTools.BuildMonthlyCheckpoints(startDate, atTime);
    }

    [Benchmark]
    public void Hybrid()
    {
        _ = CalendarTools.PrepareMonthlyTimeAccruals(Checkpoints, PerYearDuration).ToArray();
    }
}