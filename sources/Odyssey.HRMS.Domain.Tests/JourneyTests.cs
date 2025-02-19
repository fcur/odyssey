using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Odyssey.HRMS.Domain.JourneyEntity;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.Tests;

[ExcludeFromCodeCoverage]
public sealed class JourneyTests
{
    private readonly JourneyActivityName _teamImportActivityName = new JourneyActivityName("TeamImport");
    private readonly JourneyActivityName _paidHolidayAccrualActivityName = new JourneyActivityName("PaidHolidayAccrual");
    private readonly JourneyActivityName _notifyEmployeeActivityName = new JourneyActivityName("NotifyEmployee");

    private readonly JourneyActivityEventName _employeeAddedEventName = new JourneyActivityEventName("TeamEmployeeAdded");
    private readonly JourneyActivityEventName _teamImportFailedEventName = new JourneyActivityEventName("TeamImportFailed");

    private readonly JourneyActivityEventName _paidHolidayAccruedEventName = new JourneyActivityEventName("PaidHolidayAccrued");
    private readonly JourneyActivityEventName _paidHolidayAccrualFailedEventName = new JourneyActivityEventName("PaidHolidayAccrualFailed");

    private readonly JourneyActivityEventName _notificationSentEventName = new JourneyActivityEventName("NotificationSent");
    private readonly JourneyActivityEventName _notificationFailedEventName = new JourneyActivityEventName("NotificationNotSent");

    [Fact]
    public void TeamImportActivityTemplateTest()
    {
        var teamIdDependency = new JourneyActivityTemplateDependency("TeamId", JourneyActivityTemplateDependencySource.Unset);

        var events = new[]
        {
            JourneyActivityEventTemplate.CreateSource(_employeeAddedEventName, "EmployeeId"),
            JourneyActivityEventTemplate.CreateExit(_teamImportFailedEventName, "TeamId")
        };
        var dependencies = new[] { teamIdDependency };

        var teamImportActivityHeader = JourneyActivityTemplate.Create(_teamImportActivityName, events, dependencies);

        teamImportActivityHeader.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void PaidHolidayAccrualActivityTemplateTest()
    {
        var employeeIdDependency = new JourneyActivityTemplateDependency("EmployeeId", new JourneyActivityTemplateDependencySource(_teamImportActivityName, _employeeAddedEventName));

        var events = new[]
        {
            JourneyActivityEventTemplate.CreateAction(_paidHolidayAccruedEventName, "Balance", "AmountAdded"),
            JourneyActivityEventTemplate.CreateExit(_paidHolidayAccrualFailedEventName, "Balance")
        };
        var dependencies = new[] { employeeIdDependency };

        var paidHolidayAccrualActivityHeader = JourneyActivityTemplate.Create(_paidHolidayAccrualActivityName, events, dependencies);

        paidHolidayAccrualActivityHeader.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void NotifyEmployeeActivityTemplateTest()
    {
        var employeeIdDependency = new JourneyActivityTemplateDependency("EmployeeId",
            new JourneyActivityTemplateDependencySource(_teamImportActivityName, _employeeAddedEventName));
        var balanceDependency = new JourneyActivityTemplateDependency("Balance",
            new JourneyActivityTemplateDependencySource(_paidHolidayAccrualActivityName, _paidHolidayAccruedEventName));
        var amountAddedDependency = new JourneyActivityTemplateDependency("AmountAdded",
            new JourneyActivityTemplateDependencySource(_paidHolidayAccrualActivityName, _paidHolidayAccruedEventName));

        var events = new[]
        {
            JourneyActivityEventTemplate.CreateExit(_notificationSentEventName, "AtTime"),
            JourneyActivityEventTemplate.CreateExit(_notificationFailedEventName, "AtTime")
        };

        var dependencies = new[] { employeeIdDependency, balanceDependency, amountAddedDependency };

        var notifyEmployeeActivityHeader = JourneyActivityTemplate.Create(_notifyEmployeeActivityName, events, dependencies);

        notifyEmployeeActivityHeader.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Team1PaidHolidayEveryMonthAccrualFlowTest()
    {
        var nextDay = DateTimeOffset.UtcNow.AddDays(1);
        var name = new JourneyName("Paid holiday accrual every month");
        var startup = JourneyStartup.RepeatMonthlyAfterStart(nextDay);

        var teamImportId = JourneyActivityId.New();
        var paidHolidayAccrualId = JourneyActivityId.New();
        var notifyEmployeeId = JourneyActivityId.New();
        var endOfJourneyId = JourneyActivityId.New();
        _ = JourneyActivityId.New();
        var teamId = Guid.NewGuid();
        var initializationData = JourneyInitializationData.Create("TeamId", JsonSerializer.SerializeToElement(teamId));

        JourneyActivityEvent[] teamImportActivityEvents =
        [
            new JourneyActivityEvent(_employeeAddedEventName, JourneyActivityEventType.Source, paidHolidayAccrualId),
            new JourneyActivityEvent(_teamImportFailedEventName, JourneyActivityEventType.Exit, endOfJourneyId)
        ];

        JourneyActivityEvent[] paidHolidayAccrualActivityEvents =
        [
            new JourneyActivityEvent(_paidHolidayAccruedEventName, JourneyActivityEventType.Action, notifyEmployeeId),
            new JourneyActivityEvent(_paidHolidayAccrualFailedEventName, JourneyActivityEventType.Exit, endOfJourneyId)
        ];
        
        JourneyActivityEvent[] notifyEmployeeActivityEvents =
        [
            new JourneyActivityEvent(_notificationSentEventName, JourneyActivityEventType.Exit, endOfJourneyId),
            new JourneyActivityEvent(_notificationFailedEventName, JourneyActivityEventType.Exit, endOfJourneyId)
        ];

        JourneyActivity[] activities =
        [
            JourneyActivity.Create(
                teamImportId,
                _teamImportActivityName,
                JourneyActivityStatus.Draft,
                teamImportActivityEvents).GetValueOrDefault(),
            JourneyActivity.Create(
                paidHolidayAccrualId,
                _paidHolidayAccrualActivityName,
                JourneyActivityStatus.Draft,
                paidHolidayAccrualActivityEvents).GetValueOrDefault(),
            JourneyActivity.Create(
                notifyEmployeeId,
                _notifyEmployeeActivityName,
                JourneyActivityStatus.Draft,
                notifyEmployeeActivityEvents).GetValueOrDefault(),
            JourneyActivity.CreateEndOfJourney(endOfJourneyId)
        ];

        var journeyResult = Journey.Create(name, activities, startup, initializationData);

        journeyResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EmployeePaidHolidayRequestFlowTest()
    {
        
    }
}