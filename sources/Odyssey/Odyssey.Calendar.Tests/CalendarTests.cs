using System.Diagnostics.CodeAnalysis;
using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Odyssey.Calendar.Tests;

[ExcludeFromCodeCoverage]
public sealed class CalendarTests
{
    [Theory, AutoData]
    public void UserAutoDataTest(User user)
    {
        using var scope = new AssertionScope();
        user.Name.Values.Should().NotBeEmpty();
        user.Email.Value.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineAutoData("2022-01-02")]
    [InlineAutoData("2022-02-22")]
    [InlineAutoData("2022-03-05")]
    [InlineAutoData("2022-04-29")]
    [InlineAutoData("2022-05-11")]
    [InlineAutoData("2022-10-02")]
    [InlineAutoData("2022-12-31")]
    [InlineAutoData("2023-01-01")]
    [InlineAutoData("2024-01-01")]
    public void YearlyPaidTimeOffTest(string dateFromData, User user)
    {
        var paidTimeOffDuration = TimeSpan.Parse("20.00:00:00");
        var timeOffSettings = TimeOffSettings.Create(paidTimeOffDuration, TimeSpan.FromSeconds(1));
        var startDate = new StartDate(DateTimeOffset.Parse(dateFromData));
        var actor = CalendarActor.Crete(user, startDate, timeOffSettings);
        var atTime = startDate.Value.AddYears(1);

        var timeOffAdditionEvents = actor.Value.GetTimeOffAdditionEvents(atTime);
        var paidTimeOffDurationResult = CalendarEngine.CalculateTimeOffDuration(timeOffAdditionEvents.Value);
        
        using var scope = new AssertionScope();
        user.Name.Values.Should().NotBeEmpty();
        user.Email.Value.Should().NotBeNullOrEmpty();
        paidTimeOffDurationResult.Days.Should().Be(paidTimeOffDuration.Days);
        paidTimeOffDurationResult.Hours.Should().Be(0);
        paidTimeOffDurationResult.Minutes.Should().Be(0);
        paidTimeOffDurationResult.Seconds.Should().BeInRange(0, 1);
    }

    [Theory, AutoData]
    public void LeapYearPaidTimeOffTest(User user)
    {
        using var scope = new AssertionScope();
        user.Name.Values.Should().NotBeEmpty();
        user.Email.Value.Should().NotBeNullOrEmpty();
    }

    [Theory, AutoData]
    public void RegularYearPaidTimeOffTest(User user)
    {
        using var scope = new AssertionScope();
        user.Name.Values.Should().NotBeEmpty();
        user.Email.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ManualTest()
    {
        var userName = new UserName("John", "Fitzgerald", "Kennedy");
        var email = new Email("foo@bar.com");
        var user = User.Create(userName, email);
        var timeOffSettings = TimeOffSettings.CreateDefault();
        var startDate = new StartDate(DateTimeOffset.Parse("2023-02-13"));

        var actor = CalendarActor.Crete(user, startDate, timeOffSettings);

        var startTime1 = DateTimeOffset.Parse("2023-08-01");
        var startTime2 = DateTimeOffset.Parse("2023-10-01");
        var startTime3 = DateTimeOffset.Parse("2024-01-03");
        var startTime4 = DateTimeOffset.Parse("2024-05-03");

        var timeOffRequests = new[]
        {
            TimeOffRequest.CreatePaidRequest(new TimeOffRequestSettings(startTime1, TimeSpan.FromDays(5))),
            TimeOffRequest.CreatePaidRequest(new TimeOffRequestSettings(startTime2, TimeSpan.FromDays(3))),
            TimeOffRequest.CreatePaidRequest(new TimeOffRequestSettings(startTime3, TimeSpan.FromDays(1))),
            TimeOffRequest.CreatePaidRequest(new TimeOffRequestSettings(startTime4, TimeSpan.FromDays(1)))
        };

        var atTime = DateTimeOffset.Parse("2024-12-05");
        var engine = CalendarEngine.Create(actor.Value, timeOffRequests);
        var timeOffResult = engine.Value.CalculateTimeOff(atTime);

        using var scope = new AssertionScope();
        timeOffResult.IsSuccess.Should().BeTrue();
        timeOffResult.Value.PaidTimeOff.Value.Days.Should().BeGreaterThan(0);
        timeOffResult.Value.UnpaidTimeOff.Value.Days.Should().Be(0);
        timeOffResult.Value.FamilyTimeOff.Value.Days.Should().Be(0);
    }
}