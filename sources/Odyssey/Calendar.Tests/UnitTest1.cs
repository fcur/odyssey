using FluentAssertions;
using FluentAssertions.Execution;

namespace Calendar.Tests;


public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var userName = new UserName("John", "Fitzgerald", "Kennedy");
        var startDate = new StartDate(DateTimeOffset.Parse("2023-02-01"));
        var email = new Email("foo@bar.com");
        var user = User.Create(userName, email);
        var timeOffSettings = TimeOffSettings.CreateDefault();

        var actor = Actor.Crete(user, startDate, timeOffSettings);

        var startTime1 = DateTimeOffset.Parse("2023-08-01");
        var startTime2 = DateTimeOffset.Parse("2023-10-01");
        var startTime3 = DateTimeOffset.Parse("2024-01-03");
        var startTime4 = DateTimeOffset.Parse("2024-05-03");
        var duration1 = TimeSpan.FromDays(5);
        var duration2 = TimeSpan.FromDays(3);
        var duration3 = TimeSpan.FromDays(1);
        var duration4 = TimeSpan.FromDays(1);

        var timeOffRequests = new[]
        {
            PaidTimeOffRequest.Create(new TimeOffRequestSettings(startTime1, duration1), startTime1.AddDays(-5)),
            PaidTimeOffRequest.Create(new TimeOffRequestSettings(startTime2, duration2), startTime2.AddDays(-5)),
            PaidTimeOffRequest.Create(new TimeOffRequestSettings(startTime3, duration3), startTime3.AddDays(-5)),
            PaidTimeOffRequest.Create(new TimeOffRequestSettings(startTime4, duration4), startTime4.AddDays(-5))
        };

        var atTime = DateTimeOffset.Parse("2024-12-05");
        
        var engine = Engine.Create(actor, timeOffRequests);

        var timeOff = engine.CalculateTimeOff(atTime);

        using var scope = new AssertionScope();
        timeOff.PaidTimeOff.Duration.Days.Should().BeGreaterThan(0);
        timeOff.UnPaidTimeOff.Duration.Days.Should().Be(0);
        timeOff.FamilyTimeOff.Duration.Days.Should().Be(0);

    }
}