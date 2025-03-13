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
            .ToImmutableDictionary(v => v.Key, v => v.Value);
        _activityTemplatesMap = templates.ToImmutableDictionary(v => v.Name, v => v);
    }

    [Fact]
    public void ShouldStartStoryAndMoveNext()
    {
        var journeyActivityId = _teamImportActivityId;
        var journeyStoryId = JourneyStoryId.New();
        var atTime = DateTimeOffset.UtcNow;
        var storyState = BuildState(journeyActivityId);
        var journeyStory = JourneyStory.Create(journeyStoryId, storyState);
        var container1 =  RawDataContainer.Create()
            .With(TestSource.TeamIdResultKey, _teamId)
            .With(TestSource.EmployeeIdResultKey, _employeeId.Value);

        var storyEvent = new JourneyStoryEvent(journeyActivityId, _employeeAddedEventName, new StoryEventBody(container1.GetData()));
        var storyEventContext = BuildEventContext(storyEvent);
        var maybeError = journeyStory.Handle(storyEvent, storyEventContext, atTime);
        var events = journeyStory.GetEvents();
        var storyStartedEvent = events.Length > 0 ? events[0] as JourneyStoryStartedEvent: null;
        var activityStartingEvent = events.Length > 1 ? events[1] as JourneyStoryActivityStartingEvent: null;

        using var scope = new AssertionScope();
        maybeError.HasNoValue.Should().BeTrue();
        events.Length.Should().Be(2);

        storyStartedEvent.Should().NotBeNull();
        storyStartedEvent!.StoryId.Should().Be(journeyStoryId);
        storyStartedEvent.ActivityId.Should().Be(_teamImportActivityId);
        storyStartedEvent.ActivityName.Should().Be(_teamImportActivityName);
        storyStartedEvent.EventName.Should().Be(_employeeAddedEventName);
        storyStartedEvent.EventType.Should().Be(JourneyActivityEventType.Source);
        storyStartedEvent.EventBody.Should().NotBeNull();
        storyStartedEvent.EventBody!.Data.Should().ContainKey(TestSource.TeamIdResultKey);
        storyStartedEvent.EventBody.Data.Should().ContainKey(TestSource.EmployeeIdResultKey);
        storyStartedEvent.Version.Should().Be(new DomainVersion(1UL));
        
        activityStartingEvent.Should().NotBeNull();
        activityStartingEvent!.StoryId.Should().Be(journeyStoryId);
        activityStartingEvent.ActivityId.Should().Be(_paidHolidayAccrualActivityId);
        activityStartingEvent.ActivityName.Should().Be(_paidHolidayAccrualActivityName);
        activityStartingEvent.ActivityData.Should().NotBeNull();
        activityStartingEvent.ActivityData.Data.Should().ContainKey(TestSource.EmployeeIdResultKey);
        activityStartingEvent.Version.Should().Be(new DomainVersion(2UL));
        
        // var state = journeyStory.GetState();
        // state.Should().NotBeNull();
        // state.EmployeeId.Should().Be(_employeeId);
        // state.JourneyId.Should().Be(_journeys.Keys.First());
        // state.GetData<Guid>(TestSource.TeamIdResultKey).GetValueOrDefault().Should().Be(_teamId);
        // state.GetData<Guid>(TestSource.TeamIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_teamId);
        // state.GetData<Guid>(TestSource.EmployeeIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_employeeId.Value);
    }

    [Fact]
    public void ShouldHandleStartedActivity()
    {
        var container1 = RawDataContainer.Create().With(TestSource.TeamIdResultKey, _teamId).With(TestSource.EmployeeIdResultKey, _employeeId.Value);
        var container2 = RawDataContainer.Create().With(TestSource.EmployeeIdResultKey, _employeeId.Value);
        var atTime = DateTimeOffset.UtcNow;
        var journeyStoryId = JourneyStoryId.New();

        var storyStartedEvent = new JourneyStoryStartedEvent(journeyStoryId, _teamImportActivityId, _teamImportActivityName, _employeeAddedEventName, 
            JourneyActivityEventType.Source, new StoryEventBody(container1.GetData()), atTime, new DomainVersion(1L));
        var paidHolidayAccrualStartingEvent = new JourneyStoryActivityStartingEvent(journeyStoryId, _paidHolidayAccrualActivityId,
            _paidHolidayAccrualActivityName, new StoryActivityData(container2.GetData()), atTime, new DomainVersion(2L));
        
        var journeyActivityId = _paidHolidayAccrualActivityId;
        var storyState = BuildState(journeyActivityId);
        var journeyStory = JourneyStory.Create(journeyStoryId, storyState, [storyStartedEvent, paidHolidayAccrualStartingEvent]);
        
        var storyEvent = new JourneyStoryEvent(journeyActivityId, JourneyActivityEventName.ActivityStarted, StoryEventBody.Unset);
        var storyEventContext = BuildEventContext(storyEvent);
        var maybeError = journeyStory.Handle(storyEvent, storyEventContext, atTime);
        var events = journeyStory.GetEvents();
        var activityStartedEvent = events[^1] as JourneyStoryActivityStartedEvent;
        
        using var scope = new AssertionScope();
        maybeError.HasNoValue.Should().BeTrue();
        events.Length.Should().Be(3);
        activityStartedEvent.Should().NotBeNull();
        activityStartedEvent!.StoryId.Should().Be(journeyStoryId);
        activityStartedEvent.ActivityId.Should().Be(journeyActivityId);
        activityStartedEvent.ActivityName.Should().Be(_paidHolidayAccrualActivityName);
        activityStartedEvent.EventName.Should().Be(JourneyActivityEventName.ActivityStarted);
        activityStartedEvent.Body.Should().BeNull();
        activityStartedEvent.CreatedAt.Should().Be(atTime);
        activityStartedEvent.Version.Should().Be(new DomainVersion(3UL));
    }

    // var employeeAddedEvent = new JourneyStoryCompletedEvent(journeyStoryId, _teamImportActivityId, _teamImportActivityName,
    // _employeeAddedEventName, JourneyActivityEventType.Source, new StoryEventBody(data1), atTime, DomainVersion.New);
    
    private JourneyStoryEventContext BuildEventContext(JourneyStoryEvent storyEvent)
    {
        var activityId = storyEvent.Id;
        _ = _activitiesMap.TryGetValue(activityId, out var journeyId);
        _ = _journeys.TryGetValue(journeyId, out var journey);
        var initializationData = journey!.InitializationData;
        var activity = journey!.Activities.Single(v => v.Id == activityId);
        var activityEvent = activity.Events.Single(v => v.Name == storyEvent.Name);
        var activityName = activity.Name;
        var eventType = activityEvent.Type;
        var nextActivityId = activityEvent.NextActivityId;
        var nextActivity = journey!.Activities.SingleOrDefault(v => v.Id == nextActivityId);
        JourneyActivityTemplate? nextActivityTemplate = null;
        if (nextActivity != null)
        {
            _ = _activityTemplatesMap.TryGetValue(nextActivity?.Name, out nextActivityTemplate);
        }
        var nextActivityDependencies = nextActivityTemplate?.Dependencies;

        var storyEventContext = new JourneyStoryEventContext(activityName, eventType, initializationData, nextActivityId, nextActivity?.Name, nextActivityDependencies);
        return storyEventContext;
    }

    private JourneyStoryState BuildState(JourneyActivityId activityId)
    {
        _ = _activitiesMap.TryGetValue(activityId, out var journeyId);
        _ = _journeys.TryGetValue(journeyId, out var journey);

        var activities = journey!.Activities.Select(v => new JourneyStoryActivity(v.Id, v.Name, JourneyStoryActivityStatus.Ready))
            .ToDictionary(v => v.Id, v => v);

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
        var result = JourneyActivity.Create(_paidHolidayAccrualActivityId, _paidHolidayAccrualActivityName, JourneyActivityStatus.Draft,
            paidHolidayAccrualActivityEvents);
        return result.GetValueOrDefault();
    }

    private JourneyActivity BuildNotifyEmployeeActivity()
    {
        var notifyEmployeeActivityEvents = BuildNotifyEmployeeActivityEvents();
        var result = JourneyActivity.Create(_notifyEmployeeActivityId, _notifyEmployeeActivityName, JourneyActivityStatus.Draft,
            notifyEmployeeActivityEvents);
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
        var employeeIdDependency = new JourneyActivityTemplateDependency(TestSource.EmployeeIdDependencyKey,
            new JourneyActivityTemplateDependencySource(_teamImportActivityName, _employeeAddedEventName));

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

    private JourneyActivityEvent[] BuildTeamImportActivityEvents() =>
    [
        new JourneyActivityEvent(_employeeAddedEventName, JourneyActivityEventType.Source, _paidHolidayAccrualActivityId),
        new JourneyActivityEvent(_teamImportFailedEventName, JourneyActivityEventType.Completion, _endOfJourneyActivityId)
    ];

    private JourneyActivityEvent[] BuildPaidHolidayAccrualActivityEvents() =>
    [
        JourneyActivityEvent.ActivityStarted,
        new JourneyActivityEvent(_paidHolidayAccruedEventName, JourneyActivityEventType.Action, _notifyEmployeeActivityId),
        new JourneyActivityEvent(_paidHolidayAccrualFailedEventName, JourneyActivityEventType.Completion, _endOfJourneyActivityId)
    ];

    private JourneyActivityEvent[] BuildNotifyEmployeeActivityEvents() =>
    [
        // JourneyActivityEvent.ActivityStarted,
        new JourneyActivityEvent(_notificationSentEventName, JourneyActivityEventType.Completion, _endOfJourneyActivityId),
        new JourneyActivityEvent(_notificationFailedEventName, JourneyActivityEventType.Completion, _endOfJourneyActivityId)
    ];

    internal sealed class RawDataContainer(Dictionary<string, JsonElement> data)
    {
        public static RawDataContainer Create(string key, JsonElement rawData)
        {
            var data = new Dictionary<string, JsonElement>() { { key, rawData } };
            return new RawDataContainer(data);
        }
        
        public static RawDataContainer Create()
        {
            var data = new Dictionary<string, JsonElement> { };
            return new RawDataContainer(data);
        }
        
        public RawDataContainer With<TValue>(string key, TValue value)
        {
            data[key] = JsonSerializer.SerializeToElement(value); 
            return this;
        }

        public IReadOnlyDictionary<string, JsonElement> GetData() => data;
    }
}