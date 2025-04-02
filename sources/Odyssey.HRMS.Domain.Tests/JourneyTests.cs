using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FluentAssertions;
using Odyssey.HRMS.Domain.JourneyEntity;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.Tests;

[ExcludeFromCodeCoverage]
public sealed class JourneyTests
{
    private readonly JourneyActivityName _teamImportActivityName = new JourneyActivityName(TestSource.TeamImportActivityName);
    private readonly JourneyActivityName _paidHolidayAccrualActivityName = new JourneyActivityName(TestSource.PaidHolidayAccrualActivityName);
    private readonly JourneyActivityName _notifyEmployeeActivityName = new JourneyActivityName(TestSource.NotifyEmployeeActivityName);

    private readonly JourneyActivityEventName _employeeAddedEventName = new JourneyActivityEventName(TestSource.EmployeeAddedEventName);
    private readonly JourneyActivityEventName _teamImportFailedEventName = new JourneyActivityEventName(TestSource.TeamImportFailedEventName);

    private readonly JourneyActivityEventName _paidHolidayAccruedEventName = new JourneyActivityEventName(TestSource.PaidHolidayAccruedEventName);
    private readonly JourneyActivityEventName _paidHolidayAccrualFailedEventName = new JourneyActivityEventName(TestSource.PaidHolidayAccrualFailedEventName);

    private readonly JourneyActivityEventName _notificationSentEventName = new JourneyActivityEventName(TestSource.NotificationSentEventName);
    private readonly JourneyActivityEventName _notificationFailedEventName = new JourneyActivityEventName(TestSource.NotificationFailedEventName);

    [Fact]
    public void TeamImportActivityTemplateTest()
    {
        var teamIdDependency = new JourneyActivityTemplateDependency(TestSource.TeamIdDependencyKey, JourneyActivityTemplateDependencySource.Unset);

        var events = new[]
        {
            JourneyActivityEventTemplate.CreateSource(_employeeAddedEventName, TestSource.EmployeeIdResultKey),
            JourneyActivityEventTemplate.CreateExit(_teamImportFailedEventName, TestSource.TeamIdResultKey)
        };
        var dependencies = new[] { teamIdDependency };

        var teamImportActivityTemplate = JourneyActivityTemplate.Create(_teamImportActivityName, events, dependencies);

        teamImportActivityTemplate.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void PaidHolidayAccrualActivityTemplateTest()
    {
        var employeeIdDependency = new JourneyActivityTemplateDependency(TestSource.EmployeeIdDependencyKey, new JourneyActivityTemplateDependencySource(_teamImportActivityName, _employeeAddedEventName));

        var events = new[]
        {
            JourneyActivityEventTemplate.CreateAction(_paidHolidayAccruedEventName, TestSource.BalanceResultKey, TestSource.AmountAddedResultKey),
            JourneyActivityEventTemplate.CreateExit(_paidHolidayAccrualFailedEventName, TestSource.BalanceResultKey)
        };
        var dependencies = new[] { employeeIdDependency };

        var paidHolidayAccrualActivityTemplate = JourneyActivityTemplate.Create(_paidHolidayAccrualActivityName, events, dependencies);

        paidHolidayAccrualActivityTemplate.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void NotifyEmployeeActivityTemplateTest()
    {
        var employeeIdDependency = new JourneyActivityTemplateDependency(TestSource.EmployeeIdDependencyKey,
            new JourneyActivityTemplateDependencySource(_teamImportActivityName, _employeeAddedEventName));
        var balanceDependency = new JourneyActivityTemplateDependency(TestSource.BalanceDependencyKey,
            new JourneyActivityTemplateDependencySource(_paidHolidayAccrualActivityName, _paidHolidayAccruedEventName));
        var amountAddedDependency = new JourneyActivityTemplateDependency(TestSource.AmountAddedDependencyKey,
            new JourneyActivityTemplateDependencySource(_paidHolidayAccrualActivityName, _paidHolidayAccruedEventName));

        var events = new[]
        {
            JourneyActivityEventTemplate.CreateExit(_notificationSentEventName, TestSource.AtTimeResultKey),
            JourneyActivityEventTemplate.CreateExit(_notificationFailedEventName, TestSource.AtTimeResultKey)
        };

        var dependencies = new[] { employeeIdDependency, balanceDependency, amountAddedDependency };

        var notifyEmployeeActivityTemplate = JourneyActivityTemplate.Create(_notifyEmployeeActivityName, events, dependencies);

        notifyEmployeeActivityTemplate.IsSuccess.Should().BeTrue();
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
        var initializationData = JourneyInitializationData.Create(TestSource.TeamIdResultKey, JsonSerializer.SerializeToElement(teamId));

        JourneyActivityEvent[] teamImportActivityEvents =
        [
            new JourneyActivityEvent(_employeeAddedEventName, JourneyActivityEventType.Source, paidHolidayAccrualId),
            new JourneyActivityEvent(_teamImportFailedEventName, JourneyActivityEventType.Fail, endOfJourneyId)
        ];

        JourneyActivityEvent[] paidHolidayAccrualActivityEvents =
        [
            new JourneyActivityEvent(_paidHolidayAccruedEventName, JourneyActivityEventType.Success, notifyEmployeeId),
            new JourneyActivityEvent(_paidHolidayAccrualFailedEventName, JourneyActivityEventType.Fail, endOfJourneyId)
        ];
        
        JourneyActivityEvent[] notifyEmployeeActivityEvents =
        [
            new JourneyActivityEvent(_notificationSentEventName, JourneyActivityEventType.Fail, endOfJourneyId),
            new JourneyActivityEvent(_notificationFailedEventName, JourneyActivityEventType.Fail, endOfJourneyId)
        ];

        JourneyActivity[] activities =
        [
            JourneyActivity.Create(
                teamImportId,
                _teamImportActivityName,
                JourneyActivityStatus.Draft,
                teamImportActivityEvents,
                JourneyActivityTtl.Unset).GetValueOrDefault(),
            JourneyActivity.Create(
                paidHolidayAccrualId,
                _paidHolidayAccrualActivityName,
                JourneyActivityStatus.Draft,
                paidHolidayAccrualActivityEvents,
                JourneyActivityTtl.Unset).GetValueOrDefault(),
            JourneyActivity.Create(
                notifyEmployeeId,
                _notifyEmployeeActivityName,
                JourneyActivityStatus.Draft,
                notifyEmployeeActivityEvents,
                JourneyActivityTtl.Unset).GetValueOrDefault(),
            JourneyActivity.CreateEndOfJourney(endOfJourneyId)
        ];

        var journeyResult = Journey.Create(name, activities, startup, initializationData);

        journeyResult.IsSuccess.Should().BeTrue();
    }

    [Fact(Skip = "TBD")]
    public void EmployeePaidHolidayRequestFlowTest()
    {
        
    }
}