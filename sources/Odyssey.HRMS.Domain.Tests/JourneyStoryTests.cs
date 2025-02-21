using System.Diagnostics.CodeAnalysis;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;
using Odyssey.HRMS.Domain.JourneyEntity.Story;

namespace Odyssey.HRMS.Domain.Tests;

[ExcludeFromCodeCoverage]
public sealed class JourneyStoryTests
{
    private readonly JourneyActivityId _teamImportId = JourneyActivityId.New();
    private readonly JourneyActivityId _paidHolidayAccrualId = JourneyActivityId.New();
    private readonly JourneyActivityId _notifyEmployeeId = JourneyActivityId.New();
    private readonly JourneyActivityId _endOfJourneyId = JourneyActivityId.New();

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
    public void Test1()
    {
        var journeyStoryId = JourneyStoryId.New();
        var events = new DomainEvent[] { };

        var journeyStory = JourneyStory.Create(journeyStoryId, events);
    }
}
