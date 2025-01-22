using System.Diagnostics.CodeAnalysis;
using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;
using Odyssey.Domain.EmployeeEntity;
using Odyssey.Domain.TimeMachineEntity;
using Odyssey.Domain.UserEntity;

namespace Odyssey.Domain.Tests;

[ExcludeFromCodeCoverage]
public sealed class TimeMachineTests
{
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
        var paidTimeOffAccrual = new TimeOffAccrual(paidTimeOffDuration, TimeOffAccrualPolicy.Yearly);
        var paidTimeOffSettings = LeaveSettings.CreatePaidTimeOff(paidTimeOffAccrual, null);

        var leaveSettings = new[] { paidTimeOffSettings };
        var startDate = new StartDate(DateTimeOffset.Parse(dateFromData));
        var employee = Employee.Create(EmployeeId.Empty, user, startDate, leaveSettings)
            .GetValueOrDefault();
        var atTime = startDate.Value.AddYears(1);

        var timeMachine = TimeMachine.Create(employee, Array.Empty<TimeOffRequest>()).GetValueOrDefault();
        var state = timeMachine.GoTo(atTime).GetValueOrDefault();

        using var scope = new AssertionScope();
        user.Name.Values.Should().NotBeEmpty();
        user.Email.Value.Should().NotBeNullOrEmpty();

        state.Should().NotBeNull();
        state.AtTime.Should().Be(atTime);
        state.AggregatedTime.Should().ContainKey(LeaveType.PaidTimeOff);
        state[LeaveType.PaidTimeOff].Accrued.Days.Should().Be(paidTimeOffDuration.Days);
        state[LeaveType.PaidTimeOff].Accrued.Hours.Should().Be(0);
        state[LeaveType.PaidTimeOff].Accrued.Minutes.Should().Be(0);
        state[LeaveType.PaidTimeOff].Accrued.Seconds.Should().BeInRange(0, 1);
    }
    
    [Fact]
    public void ManualTest()
    {
        var userName = new UserName("John", "Fitzgerald", "Kennedy");
        var email = new Email("foo@bar.com");
        var user = User.Create(UserId.Empty, userName, email);
        var leaveSettings = LeaveSettings.CreateDefault();
        var startDate = new StartDate(DateTimeOffset.Parse("2023-02-13"));

        var actor = Employee.Create(EmployeeId.Empty, user, startDate, leaveSettings)
            .GetValueOrDefault();

        var startTime1 = DateTimeOffset.Parse("2023-08-01");
        var startTime2 = DateTimeOffset.Parse("2023-10-01");
        var startTime3 = DateTimeOffset.Parse("2024-01-03");
        var startTime4 = DateTimeOffset.Parse("2024-05-03");

        var timeOffRequests = new[]
        {
            TimeOffRequest.CreatePaidTimeOffRequest(new TimeOffRequestSettings(startTime1, TimeSpan.FromDays(5))),
            TimeOffRequest.CreatePaidTimeOffRequest(new TimeOffRequestSettings(startTime2, TimeSpan.FromDays(3))),
            TimeOffRequest.CreatePaidTimeOffRequest(new TimeOffRequestSettings(startTime3, TimeSpan.FromDays(1))),
            TimeOffRequest.CreatePaidTimeOffRequest(new TimeOffRequestSettings(startTime4, TimeSpan.FromDays(1)))
        };

        var atTime = DateTimeOffset.Parse("2024-12-05");
        var timeMachine = TimeMachine.Create(actor, timeOffRequests).GetValueOrDefault();
        var state = timeMachine.GoTo(atTime).GetValueOrDefault();

        using var scope = new AssertionScope();
        
        state.Should().NotBeNull();
        state.AtTime.Should().Be(atTime);
        state.AggregatedTime.Should().ContainKey(LeaveType.PaidTimeOff);
        state.AggregatedTime.Should().ContainKey(LeaveType.UnpaidTimeOff);
        state.AggregatedTime.Should().NotContainKey(LeaveType.FamilyLeave);
        state[LeaveType.PaidTimeOff].Accrued.Days.Should().BeGreaterThan(0);
        state[LeaveType.PaidTimeOff].Used.Days.Should().BeGreaterThan(0);
        state[LeaveType.PaidTimeOff].Delta.Days.Should().BeGreaterThan(0);
        state[LeaveType.UnpaidTimeOff].Accrued.Days.Should().BeGreaterThan(0);
        state[LeaveType.UnpaidTimeOff].Used.Days.Should().Be(0);
        state[LeaveType.UnpaidTimeOff].Delta.Days.Should().BeGreaterThan(0);
    }
}