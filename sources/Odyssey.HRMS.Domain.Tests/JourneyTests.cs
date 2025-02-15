using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
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
        
        var events = new[] { 
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
        var employeeIdDependency = new JourneyActivityTemplateDependency("EmployeeId", 
            new JourneyActivityTemplateDependencySource(_teamImportActivityName, _employeeAddedEventName));
        
        var events = new[] { 
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
            
        var events = new[] { 
            JourneyActivityEventTemplate.CreateExit(_notificationSentEventName, "AtTime"), 
            JourneyActivityEventTemplate.CreateExit(_notificationFailedEventName, "AtTime") 
        };
        
        var dependencies = new[] { employeeIdDependency, balanceDependency, amountAddedDependency};

        var notifyEmployeeActivityHeader = JourneyActivityTemplate.Create(_notifyEmployeeActivityName, events, dependencies);

        notifyEmployeeActivityHeader.IsSuccess.Should().BeTrue();
    }
    
    [Fact]
    public void Team1PaidHolidayEveryMonthAccrualFlowTest()
    {
        
    }

    [Fact]
    public void EmployeePaidHolidayRequestFlowTest()
    {
        
    }
}