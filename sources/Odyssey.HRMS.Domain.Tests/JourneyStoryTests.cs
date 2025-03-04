using FluentAssertions;
using FluentAssertions.Execution;
using Odyssey.HRMS.Domain.Base;
using System.Diagnostics.CodeAnalysis;
using Odyssey.HRMS.Domain.EmployeeEntity;
using Odyssey.HRMS.Domain.JourneyEntity;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;
using Odyssey.HRMS.Domain.JourneyEntity.Story;
using System.Collections.Immutable;
using System.Text.Json;
// ReSharper disable NullableWarningSuppressionIsUsed
#pragma warning disable CS8604 // Possible null reference argument.

namespace Odyssey.HRMS.Domain.Tests;

[ExcludeFromCodeCoverage]
public sealed class JourneyStoryTests
{
    private readonly JourneyActivityId _teamImportActivityId = JourneyActivityId.New();
    private readonly JourneyActivityId _paidHolidayAccrualActivityId = JourneyActivityId.New();
    private readonly JourneyActivityId _notifyEmployeeActivityId = JourneyActivityId.New();
    private readonly JourneyActivityId _endOfJourneyActivityId = JourneyActivityId.New();
    private readonly JourneyActivityName _teamImportActivityName = new JourneyActivityName(TestSource.TeamImportActivityName);
    private readonly JourneyActivityName _paidHolidayAccrualActivityName = new JourneyActivityName(TestSource.PaidHolidayAccrualActivityName);
    private readonly JourneyActivityName _notifyEmployeeActivityName = new JourneyActivityName(TestSource.NotifyEmployeeActivityName);
    private readonly JourneyActivityEventName _employeeAddedEventName = new JourneyActivityEventName(TestSource.EmployeeAddedEventName);
    private readonly JourneyActivityEventName _teamImportFailedEventName = new JourneyActivityEventName(TestSource.TeamImportFailedEventName);
    private readonly JourneyActivityEventName _paidHolidayAccruedEventName = new JourneyActivityEventName(TestSource.PaidHolidayAccruedEventName);
    private readonly JourneyActivityEventName _paidHolidayAccrualFailedEventName = new JourneyActivityEventName(TestSource.PaidHolidayAccrualFailedEventName);
    private readonly JourneyActivityEventName _notificationSentEventName = new JourneyActivityEventName(TestSource.NotificationSentEventName);
    private readonly JourneyActivityEventName _notificationFailedEventName = new JourneyActivityEventName(TestSource.NotificationFailedEventName);
    private readonly Guid _teamId = Guid.NewGuid();
    private readonly EmployeeId _employeeId = EmployeeId.New();
    private readonly ImmutableDictionary<JourneyId, Journey> _journeys;
    private readonly ImmutableDictionary<JourneyActivityId, JourneyId> _activitiesMap;
    private readonly ImmutableDictionary<JourneyActivityName, JourneyActivityTemplate> _activityTemplatesMap;
    
    public JourneyStoryTests()
    {
        var startDate = DateTimeOffset.UtcNow.AddDays(1);
        var name = new JourneyName("Paid holiday accrual every month");
        var startup = JourneyStartup.RepeatMonthlyAfterStart(startDate);
        var initializationData = JourneyInitializationData.Create(TestSource.TeamIdResultKey, JsonSerializer.SerializeToElement(_teamId));

        JourneyActivity[] activities =
        [
            BuildTeamImportActivity(),
            BuildPaidHolidayAccrualActivity(),
            BuildNotifyEmployeeActivity(),
            JourneyActivity.CreateEndOfJourney(_endOfJourneyActivityId)
        ];

        JourneyActivityTemplate[] templates =
        [
            BuildTeamImportActivityTemplate(),
            BuildPaidHolidayAccrualActivityTemplate(),
            BuildNotifyEmployeeActivityTemplate()
        ];
        
        var journeys = new[] { Journey.Create(name, activities, startup, initializationData).GetValueOrDefault() };
        
        _journeys = journeys.ToImmutableDictionary(v => v.Id, v => v);
        _activitiesMap = journeys.SelectMany(jrn => jrn.Activities.Select(act => new KeyValuePair<JourneyActivityId, JourneyId>(act.Id, jrn.Id)))
            .ToImmutableDictionary(v=>v.Key, v=>v.Value);
        _activityTemplatesMap = templates.ToImmutableDictionary(v => v.Name, v => v);
    }
    
    [Fact]
    public void Test1()
    {
        var journeyActivityId = _teamImportActivityId;
        var journeyStoryId = JourneyStoryId.New();
        var atTime = DateTimeOffset.UtcNow;
        var storyState = BuildState(journeyActivityId);
        var journeyStory = JourneyStory.Create(journeyStoryId, storyState);
        var employeeAddedEventBody = EventBody.Create()
            .With(TestSource.TeamIdResultKey, _teamId)
            .With(TestSource.EmployeeIdResultKey, _employeeId.Value);

        var storyEvent = new JourneyStoryEvent(journeyActivityId, _employeeAddedEventName, employeeAddedEventBody);
        var storyEventContext = BuildEventContext(storyEvent);
        var maybeError = journeyStory.Handle(storyEvent, storyEventContext, atTime);
        var state = journeyStory.GetState();

        using var scope = new AssertionScope();
        maybeError.HasNoValue.Should().BeTrue();
        state.Should().NotBeNull();
        state.EmployeeId.Should().Be(_employeeId);
        state.JourneyId.Should().Be(_journeys.Keys.First());
        state.GetData<Guid>(StoryDataKey.Create(TestSource.TeamIdResultKey)).GetValueOrDefault().Should().Be(_teamId);
        state.GetData<Guid>(StoryDataKey.Create(TestSource.TeamIdResultKey, _teamImportActivityName, _employeeAddedEventName)).GetValueOrDefault().Should().Be(_teamId);
        state.GetData<Guid>(StoryDataKey.Create(TestSource.EmployeeIdResultKey, _teamImportActivityName, _employeeAddedEventName)).GetValueOrDefault().Should().Be(_employeeId.Value);
    }

    [Fact]
    public void Test2()
    {  
        var atTime = DateTimeOffset.UtcNow;
        var journeyStoryId = JourneyStoryId.New();
        var employeeAddedEventBody = EventBody.Create().With(TestSource.TeamIdResultKey, _teamId)
            .With(TestSource.EmployeeIdResultKey, _employeeId.Value);
        var employeeAddedEvent = new JourneyStoryCompletedEvent(journeyStoryId, _teamImportActivityId, _teamImportActivityName, 
            _employeeAddedEventName, JourneyActivityEventType.Source, employeeAddedEventBody, atTime, DomainVersion.New);
    }

    private JourneyStoryEventContext BuildEventContext(JourneyStoryEvent storyEvent)
    {
        var activityId = storyEvent.Id;
        _  = _activitiesMap.TryGetValue(activityId, out var journeyId);
        _ = _journeys.TryGetValue(journeyId, out var journey);
        var initializationData = journey!.InitializationData;
        var activity = journey!.Activities.Single(v=>v.Id == activityId);
        var activityEvent = activity.Events.Single(v => v.Name == storyEvent.Name); 
        var activityName = activity.Name;
        var eventType = activityEvent.Type;
        var nextActivityId = activityEvent.NextActivityId;
        var nextActivity = journey!.Activities.SingleOrDefault(v=>v.Id == nextActivityId);
        _ = _activityTemplatesMap.TryGetValue(nextActivity?.Name, out var nextActivityTemplate);
        var nextActivityDependencies = nextActivityTemplate?.Dependencies;
        
        var storyEventContext = new JourneyStoryEventContext(activityName, eventType, initializationData, nextActivityId, nextActivityDependencies);
        return storyEventContext;
    }

    private JourneyStoryState BuildState(JourneyActivityId activityId)
    {
        _  = _activitiesMap.TryGetValue(activityId, out var journeyId);
        _ = _journeys.TryGetValue(journeyId, out var journey);

        var activities = journey!.Activities.Select(v => new JourneyStoryActivity(v.Id, v.Name, JourneyActivityStatus.Ready))
            .ToDictionary(v=>v.Id, v=>v);

        var data = journey.InitializationData != null
            ? journey.InitializationData!.Data.ToDictionary(v => StoryDataKey.Create(v.Key), v => v.Value)
            : new Dictionary<StoryDataKey, JsonElement>();
        
        return JourneyStoryState.Create(journeyId, activities!, data);
    }
    
    private JourneyActivity BuildTeamImportActivity()
    {
        var teamImportActivityEvents = BuildTeamImportActivityEvents();
        var result = JourneyActivity.Create(_teamImportActivityId, _teamImportActivityName, JourneyActivityStatus.Draft, teamImportActivityEvents);
        return result.GetValueOrDefault();
    }

    private JourneyActivity BuildPaidHolidayAccrualActivity()
    {
        var paidHolidayAccrualActivityEvents = BuildPaidHolidayAccrualActivityEvents();
        var result = JourneyActivity.Create(_paidHolidayAccrualActivityId, _paidHolidayAccrualActivityName, JourneyActivityStatus.Draft, paidHolidayAccrualActivityEvents);
        return result.GetValueOrDefault();
    }

    private JourneyActivity BuildNotifyEmployeeActivity()
    {
        var notifyEmployeeActivityEvents = BuildNotifyEmployeeActivityEvents();
        var result = JourneyActivity.Create(_notifyEmployeeActivityId, _notifyEmployeeActivityName, JourneyActivityStatus.Draft, notifyEmployeeActivityEvents);
        return result.GetValueOrDefault();
    }
    
    private JourneyActivityTemplate BuildTeamImportActivityTemplate()
    {
        var teamIdDependency = new JourneyActivityTemplateDependency(TestSource.TeamIdDependencyKey, JourneyActivityTemplateDependencySource.Unset);

        var events = new[]
        {
            JourneyActivityEventTemplate.CreateSource(_employeeAddedEventName, TestSource.EmployeeIdResultKey),
            JourneyActivityEventTemplate.CreateExit(_teamImportFailedEventName, TestSource.TeamIdResultKey)
        };
        var dependencies = new[] { teamIdDependency };

        var result = JourneyActivityTemplate.Create(_teamImportActivityName, events, dependencies);
        return result.GetValueOrDefault();
    }

    private JourneyActivityTemplate BuildPaidHolidayAccrualActivityTemplate()
    {
        var employeeIdDependency = new JourneyActivityTemplateDependency(TestSource.EmployeeIdDependencyKey, new JourneyActivityTemplateDependencySource(_teamImportActivityName, _employeeAddedEventName));

        var events = new[]
        {
            JourneyActivityEventTemplate.CreateAction(_paidHolidayAccruedEventName, TestSource.BalanceResultKey, TestSource.AmountAddedResultKey),
            JourneyActivityEventTemplate.CreateExit(_paidHolidayAccrualFailedEventName, TestSource.BalanceResultKey)
        };
        var dependencies = new[] { employeeIdDependency };

        var result = JourneyActivityTemplate.Create(_paidHolidayAccrualActivityName, events, dependencies);
        return result.GetValueOrDefault();
    }

    private JourneyActivityTemplate BuildNotifyEmployeeActivityTemplate()
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

        var result = JourneyActivityTemplate.Create(_notifyEmployeeActivityName, events, dependencies);
        return result.GetValueOrDefault();
    }
    
    private JourneyActivityEvent[] BuildTeamImportActivityEvents() =>  [
        new JourneyActivityEvent(_employeeAddedEventName, JourneyActivityEventType.Source, _paidHolidayAccrualActivityId),
        new JourneyActivityEvent(_teamImportFailedEventName, JourneyActivityEventType.Completion, _endOfJourneyActivityId)
    ];
    
    private JourneyActivityEvent[] BuildPaidHolidayAccrualActivityEvents() =>  [
        // JourneyActivityEvent.ActivityStarted,
        new JourneyActivityEvent(_paidHolidayAccruedEventName, JourneyActivityEventType.Action, _notifyEmployeeActivityId),
        new JourneyActivityEvent(_paidHolidayAccrualFailedEventName, JourneyActivityEventType.Completion, _endOfJourneyActivityId)
    ];
    
    private JourneyActivityEvent[] BuildNotifyEmployeeActivityEvents() =>  [
        // JourneyActivityEvent.ActivityStarted,
        new JourneyActivityEvent(_notificationSentEventName, JourneyActivityEventType.Completion, _endOfJourneyActivityId),
        new JourneyActivityEvent(_notificationFailedEventName, JourneyActivityEventType.Completion, _endOfJourneyActivityId)
    ];
}