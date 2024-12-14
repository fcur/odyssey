using System.Diagnostics.CodeAnalysis;
using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Calendar.Tests;

[ExcludeFromCodeCoverage ]
public sealed class CalendarTests
{
    [Fact]
    public void InitialManualTest()
    {
        var userName = new UserName("John", "Fitzgerald", "Kennedy");
        var email = new Email("foo@bar.com");
        var user = User.Create(userName, email);
        var timeOffSettings = TimeOffSettings.CreateDefault();
        var startDate = new StartDate(DateTimeOffset.Parse("2023-02-13"));

        var actor = Actor.Crete(user, startDate, timeOffSettings);


        var startTime1 = DateTimeOffset.Parse("2023-08-01");
        var startTime2 = DateTimeOffset.Parse("2023-10-01");
        var startTime3 = DateTimeOffset.Parse("2024-01-03");
        var startTime4 = DateTimeOffset.Parse("2024-05-03");

        var timeOffRequests = new[]
        {
            PaidTimeOffRequest.Create(new TimeOffRequestSettings(startTime1, TimeSpan.FromDays(5))),
            PaidTimeOffRequest.Create(new TimeOffRequestSettings(startTime2, TimeSpan.FromDays(3))),
            PaidTimeOffRequest.Create(new TimeOffRequestSettings(startTime3, TimeSpan.FromDays(1))),
            PaidTimeOffRequest.Create(new TimeOffRequestSettings(startTime4, TimeSpan.FromDays(1)))
        };

        var atTime = DateTimeOffset.Parse("2024-12-05");
        var timeOffAdditionEvents = actor.GetTimeOffAdditionEvents(atTime);

        var engine = Engine.Create(actor, timeOffAdditionEvents, timeOffRequests);

        var timeOff = engine.CalculateTimeOff(atTime);

        using var scope = new AssertionScope();
        timeOff.PaidTimeOff.Duration.Days.Should().BeGreaterThan(0);
        timeOff.UnPaidTimeOff.Duration.Days.Should().Be(0);
        timeOff.FamilyTimeOff.Duration.Days.Should().Be(0);
    }
    
    [Theory, AutoData]
    public void UserAutoDataTest(User user)
    {
        using var scope = new AssertionScope();
        user.UserName.Name.Should().NotBeEmpty();
        user.Email.Value.Should().NotBeNullOrEmpty();
    }
}