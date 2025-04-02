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
    private readonly JourneyActivityName _teamImportActivityName = new (TestSource.TeamImportActivityName);
    private readonly JourneyActivityName _paidHolidayAccrualActivityName = new (TestSource.PaidHolidayAccrualActivityName);
    private readonly JourneyActivityName _notifyEmployeeActivityName = new (TestSource.NotifyEmployeeActivityName);
    private readonly JourneyActivityEventName _employeeAddedEventName = new (TestSource.EmployeeAddedEventName);
    private readonly JourneyActivityEventName _teamImportFailedEventName = new (TestSource.TeamImportFailedEventName);
    private readonly JourneyActivityEventName _paidHolidayAccruedEventName = new (TestSource.PaidHolidayAccruedEventName);
    private readonly JourneyActivityEventName _paidHolidayAccrualFailedEventName = new (TestSource.PaidHolidayAccrualFailedEventName);
    private readonly JourneyActivityEventName _notificationSentEventName = new (TestSource.NotificationSentEventName);
    private readonly JourneyActivityEventName _notificationFailedEventName = new (TestSource.NotificationFailedEventName);
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
        // Arrange
        var atTime = DateTimeOffset.UtcNow;
        var journeyStoryId = JourneyStoryId.New();
        
        var storyState = BuildState(_teamImportActivityId);
        var journeyStory = JourneyStory.Create(journeyStoryId, storyState);
        var storyStartedEventData = GetStoryStartedEventData();
        var storyEvent = new JourneyStoryEvent(_teamImportActivityId, _employeeAddedEventName, storyStartedEventData);
        var storyEventContext = BuildEventContext(storyEvent);
        
        // Act
        var maybeError = journeyStory.Handle(storyEvent, storyEventContext, atTime);
        var events = journeyStory.GetEvents();
        
        var storyStartedEvent = events[^2] as JourneyStoryStartedEvent;
        var paidHolidayAccrualStartingEvent = events[^1] as JourneyStoryActivityStartingEvent;
        var (teamImportActivity, paidHolidayAccrualActivity, _,_) = GetActivities(storyState);
        
        // Assert
        using var scope = new AssertionScope();
        maybeError.HasNoValue.Should().BeTrue();
        events.Should().HaveCount(2);

        EnsureStoryStartedEvent(storyStartedEvent, journeyStoryId);
        EnsurePaidHolidayAccrualStartingEvent(paidHolidayAccrualStartingEvent, journeyStoryId);

        storyState.EmployeeId.Should().Be(_employeeId);
        storyState.JourneyId.Should().Be(_journeys.Keys.First());
        storyState.GetData<Guid>(TestSource.TeamIdResultKey).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.TeamIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.EmployeeIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_employeeId.Value);
        
        EnsureFinishedActivity(teamImportActivity);
        EnsureStartingActivity(paidHolidayAccrualActivity);
        
        journeyStory.Version.Should().Be(TestSource.Version3);
    }

    [Fact]
    public void ShouldHandlePaidHolidayAccrualStartedActivityEvent()
    {
        // Arrange
        var atTime = DateTimeOffset.UtcNow;
        var journeyStoryId = JourneyStoryId.New();
        var (storyStartedEvent, paidHolidayAccrualStartingEvent, _, _, _, _, _) = GetArrangePositiveFlowEvents(journeyStoryId, atTime);
        
        var storyState = BuildState(_paidHolidayAccrualActivityId);
        var journeyStory = JourneyStory.Create(journeyStoryId, storyState, [storyStartedEvent, paidHolidayAccrualStartingEvent]);
        var storyEvent = new JourneyStoryEvent(_paidHolidayAccrualActivityId, JourneyActivityEventName.ActivityStarted, StoryEventBody.Unset);
        var storyEventContext = BuildEventContext(storyEvent);
        
        // Act
        var maybeError = journeyStory.Handle(storyEvent, storyEventContext, atTime);
        var events = journeyStory.GetEvents();
        var paidHolidayAccrualStartedEvent = events[^1] as JourneyStoryActivityStartedEvent;
        var (teamImportActivity, paidHolidayAccrualActivity, _, _) = GetActivities(storyState);
        
        // Assert
        using var scope = new AssertionScope();
        maybeError.HasNoValue.Should().BeTrue();
        events.Should().HaveCount(3);

        EnsureStoryStartedEvent(storyStartedEvent, journeyStoryId);
        EnsurePaidHolidayAccrualStartingEvent(paidHolidayAccrualStartingEvent, journeyStoryId);
        EnsurePaidHolidayAccrualStartedEvent(paidHolidayAccrualStartedEvent, journeyStoryId);
        
        storyState.EmployeeId.Should().Be(_employeeId);
        storyState.JourneyId.Should().Be(_journeys.Keys.First());
        storyState.GetData<Guid>(TestSource.TeamIdResultKey).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.TeamIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.EmployeeIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_employeeId.Value);
        storyState.IsFinished.Should().BeFalse();
        
        EnsureFinishedActivity(teamImportActivity);
        EnsureStartedActivity(paidHolidayAccrualActivity);
        
        journeyStory.Version.Should().Be(TestSource.Version4);
    }

    [Fact]
    public void ShouldHandlePaidHolidayAccrualCompletedActivityEventAndMoveNext()
    {
        // Arrange
        var atTime = DateTimeOffset.UtcNow;
        var journeyStoryId = JourneyStoryId.New();
        var (storyStartedEvent, paidHolidayAccrualStartingEvent, paidHolidayAccrualStartedEvent, _, _, _, _) = GetArrangePositiveFlowEvents(journeyStoryId, atTime);
        var (paidHolidayAccruedEventData, balance, added) = GetPaidHolidayAccruedEventData();
        
        var storyState = BuildState(_paidHolidayAccrualActivityId);
        var journeyStory = JourneyStory.Create(journeyStoryId, storyState, [storyStartedEvent, paidHolidayAccrualStartingEvent, paidHolidayAccrualStartedEvent]);
        
        var storyEvent = new JourneyStoryEvent(_paidHolidayAccrualActivityId, _paidHolidayAccruedEventName, paidHolidayAccruedEventData);
        var storyEventContext = BuildEventContext(storyEvent);
        
        // Act
        var maybeError = journeyStory.Handle(storyEvent, storyEventContext, atTime);
        var events = journeyStory.GetEvents();
        
        var paidHolidayAccrualCompletedEvent = events[^2] as JourneyStoryActivityCompletedEvent;
        var notifyEmployeeStartingEvent = events[^1] as JourneyStoryActivityStartingEvent;
        var (teamImportActivity, paidHolidayAccrualActivity, notifyEmployeeActivity, _) = GetActivities(storyState);
        
        // Assert
        using var scope = new AssertionScope();
        maybeError.HasNoValue.Should().BeTrue();
        events.Should().HaveCount(5);

        EnsureStoryStartedEvent(storyStartedEvent, journeyStoryId);
        EnsurePaidHolidayAccrualStartingEvent(paidHolidayAccrualStartingEvent, journeyStoryId);
        EnsurePaidHolidayAccrualStartedEvent(paidHolidayAccrualStartedEvent, journeyStoryId);
        EnsurePaidHolidayAccrualCompletedEvent(paidHolidayAccrualCompletedEvent, journeyStoryId);
        EnsureNotifyEmployeeStartingEvent(notifyEmployeeStartingEvent, journeyStoryId);
        
        storyState.EmployeeId.Should().Be(_employeeId);
        storyState.JourneyId.Should().Be(_journeys.Keys.First());
        storyState.GetData<Guid>(TestSource.TeamIdResultKey).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.TeamIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.EmployeeIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_employeeId.Value);
        storyState.GetData<decimal>(TestSource.BalanceResultKey, _paidHolidayAccrualActivityName, _paidHolidayAccruedEventName).GetValueOrDefault().Should().Be(balance);
        storyState.GetData<decimal>(TestSource.AmountAddedResultKey, _paidHolidayAccrualActivityName, _paidHolidayAccruedEventName).GetValueOrDefault().Should().Be(added);
        storyState.IsFinished.Should().BeFalse();
        
        EnsureFinishedActivity(teamImportActivity);
        EnsureFinishedActivity(paidHolidayAccrualActivity);
        EnsureStartingActivity(notifyEmployeeActivity);
        
        journeyStory.Version.Should().Be(TestSource.Version6);
    }

    [Fact]
    public void ShouldHandleNotifyEmployeeStartedEvent()
    {
        // Arrange
        var atTime = DateTimeOffset.UtcNow;
        var journeyStoryId = JourneyStoryId.New();
        var (balance, added) = GetPaidHolidayAccruedData();
        var (storyStartedEvent, paidHolidayAccrualStartingEvent, paidHolidayAccrualStartedEvent, paidHolidayAccrualCompletedEvent,
            notifyEmployeeStartingEvent, _, _) = GetArrangePositiveFlowEvents(journeyStoryId, atTime);
        
        var storyState = BuildState(_notifyEmployeeActivityId);
        var journeyStory = JourneyStory.Create(journeyStoryId, storyState, [storyStartedEvent, paidHolidayAccrualStartingEvent, 
            paidHolidayAccrualStartedEvent, paidHolidayAccrualCompletedEvent, notifyEmployeeStartingEvent]);
        var storyEvent = new JourneyStoryEvent(_notifyEmployeeActivityId, JourneyActivityEventName.ActivityStarted, StoryEventBody.Unset);
        var storyEventContext = BuildEventContext(storyEvent);
        
        // Act
        var maybeError = journeyStory.Handle(storyEvent, storyEventContext, atTime);
        var events = journeyStory.GetEvents();
        var notifyEmployeeStartedEvent = events[^1] as JourneyStoryActivityStartedEvent;
        var (teamImportActivity, paidHolidayAccrualActivity, notifyEmployeeActivity, _) = GetActivities(storyState);
        
        // Assert
        using var scope = new AssertionScope();
        maybeError.HasNoValue.Should().BeTrue();
        events.Should().HaveCount(6);

        EnsureStoryStartedEvent(storyStartedEvent, journeyStoryId);
        EnsurePaidHolidayAccrualStartingEvent(paidHolidayAccrualStartingEvent, journeyStoryId);
        EnsurePaidHolidayAccrualStartedEvent(paidHolidayAccrualStartedEvent, journeyStoryId);
        EnsureNotifyEmployeeStartingEvent(notifyEmployeeStartingEvent, journeyStoryId);
        EnsureNotifyEmployeeStartedEvent(notifyEmployeeStartedEvent, journeyStoryId);
        
        storyState.EmployeeId.Should().Be(_employeeId);
        storyState.JourneyId.Should().Be(_journeys.Keys.First());
        storyState.GetData<Guid>(TestSource.TeamIdResultKey).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.TeamIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.EmployeeIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_employeeId.Value);
        storyState.GetData<decimal>(TestSource.BalanceResultKey, _paidHolidayAccrualActivityName, _paidHolidayAccruedEventName).GetValueOrDefault().Should().Be(balance);
        storyState.GetData<decimal>(TestSource.AmountAddedResultKey, _paidHolidayAccrualActivityName, _paidHolidayAccruedEventName).GetValueOrDefault().Should().Be(added);
        storyState.IsFinished.Should().BeFalse();
        
        EnsureFinishedActivity(teamImportActivity);
        EnsureFinishedActivity(paidHolidayAccrualActivity);
        EnsureStartedActivity(notifyEmployeeActivity);
        
        journeyStory.Version.Should().Be(TestSource.Version7);
    }
    
    [Fact]
    public void ShouldHandleNotifyEmployeeCompletedActivityEventAndFinalizeStory()
    {
        // Arrange
        var atTime = DateTimeOffset.UtcNow;
        var journeyStoryId = JourneyStoryId.New();
        var (balance, added) = GetPaidHolidayAccruedData();
        var notifyEmployeeSentEventData = GetNotificationSentEventData(atTime);
        var (storyStartedEvent, paidHolidayAccrualStartingEvent, paidHolidayAccrualStartedEvent, paidHolidayAccrualCompletedEvent,
            notifyEmployeeStartingEvent, notifyEmployeeStartedEvent, _) = GetArrangePositiveFlowEvents(journeyStoryId, atTime);
        
        var storyState = BuildState(_notifyEmployeeActivityId);
        var journeyStory = JourneyStory.Create(journeyStoryId, storyState, [storyStartedEvent, paidHolidayAccrualStartingEvent, 
            paidHolidayAccrualStartedEvent, paidHolidayAccrualCompletedEvent, notifyEmployeeStartingEvent, notifyEmployeeStartedEvent]);
        var storyEvent = new JourneyStoryEvent(_notifyEmployeeActivityId, _notificationSentEventName, notifyEmployeeSentEventData);
        var storyEventContext = BuildEventContext(storyEvent);
        
        // Act
        var maybeError = journeyStory.Handle(storyEvent, storyEventContext, atTime);
        var events = journeyStory.GetEvents();
        var notifyEmployeeSentEvent = events[^2] as JourneyStoryActivityCompletedEvent;
        var storyCompletedEvent = events[^1] as JourneyStoryCompletedEvent;
        var (teamImportActivity, paidHolidayAccrualActivity, notifyEmployeeActivity, endOfJourneyActivity) = GetActivities(storyState);

        // Assert
        using var scope = new AssertionScope();
        maybeError.HasNoValue.Should().BeTrue();
        events.Should().HaveCount(8);

        EnsureStoryStartedEvent(storyStartedEvent, journeyStoryId);
        EnsurePaidHolidayAccrualStartingEvent(paidHolidayAccrualStartingEvent, journeyStoryId);
        EnsurePaidHolidayAccrualStartedEvent(paidHolidayAccrualStartedEvent, journeyStoryId);
        EnsureNotifyEmployeeStartingEvent(notifyEmployeeStartingEvent, journeyStoryId);
        EnsureNotifyEmployeeStartedEvent(notifyEmployeeStartedEvent, journeyStoryId);
        EnsureNotifyEmployeeSentEvent(notifyEmployeeSentEvent, journeyStoryId);
        EnsureStoryCompletedEvent(storyCompletedEvent, journeyStoryId);
        
        storyState.EmployeeId.Should().Be(_employeeId);
        storyState.JourneyId.Should().Be(_journeys.Keys.First());
        storyState.GetData<Guid>(TestSource.TeamIdResultKey).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.TeamIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_teamId);
        storyState.GetData<Guid>(TestSource.EmployeeIdResultKey, _teamImportActivityName, _employeeAddedEventName).GetValueOrDefault().Should().Be(_employeeId.Value);
        storyState.GetData<decimal>(TestSource.BalanceResultKey, _paidHolidayAccrualActivityName, _paidHolidayAccruedEventName).GetValueOrDefault().Should().Be(balance);
        storyState.GetData<decimal>(TestSource.AmountAddedResultKey, _paidHolidayAccrualActivityName, _paidHolidayAccruedEventName).GetValueOrDefault().Should().Be(added);
        storyState.GetData<DateTimeOffset>(TestSource.AtTimeResultKey, _notifyEmployeeActivityName, _notificationSentEventName).GetValueOrDefault().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        storyState.IsFinished.Should().BeTrue();
        
        EnsureFinishedActivity(teamImportActivity);
        EnsureFinishedActivity(paidHolidayAccrualActivity);
        EnsureFinishedActivity(notifyEmployeeActivity);
        EnsureFinishedActivity(endOfJourneyActivity);
        
        journeyStory.Version.Should().Be(TestSource.Version9);
    }

    private (JourneyStoryActivity teamImport, JourneyStoryActivity paidHolidayAccrual, JourneyStoryActivity notifyEmployee, JourneyStoryActivity endOfJourney) GetActivities(JourneyStoryState storyState)
    {
        var teamImport = storyState.GetActivity(_teamImportActivityId).GetValueOrDefault();
        var paidHolidayAccrual = storyState.GetActivity(_paidHolidayAccrualActivityId).GetValueOrDefault();
        var notifyEmployee = storyState.GetActivity(_notifyEmployeeActivityId).GetValueOrDefault();
        var endOfJourney = storyState.GetActivity(_endOfJourneyActivityId).GetValueOrDefault();
        
        return (teamImport, paidHolidayAccrual, notifyEmployee, endOfJourney);
    }
    
    private StoryEventBody GetStoryStartedEventData()
    {
        var builder = DataBuilder.Create().With(TestSource.TeamIdResultKey, _teamId).With(TestSource.EmployeeIdResultKey, _employeeId.Value);

        return new StoryEventBody(builder.GetData());
    }

    private StoryActivityData GetPaidHolidayAccrualStartingActivityData()
    {
        var builder = DataBuilder.Create().With(TestSource.EmployeeIdResultKey, _employeeId.Value);
        return new StoryActivityData(builder.GetData());
    }
    
    private (decimal balance, decimal added) GetPaidHolidayAccruedData()
    {
        var balance = 13.3314M;
        var added = 1.66666666667M;
        
        return (balance, added);
    }
    
    private (StoryEventBody paidHolidayAccruedEventData, decimal balance, decimal added) GetPaidHolidayAccruedEventData()
    {
        var (balance, added) = GetPaidHolidayAccruedData();
        var builder = DataBuilder.Create().With(TestSource.BalanceResultKey, balance).With(TestSource.AmountAddedResultKey, added);
        
        return (new StoryEventBody(builder.GetData()), balance, added);
    }
    
    private StoryActivityData GetNotifyEmployeeStartingEventData(decimal balance, decimal added)
    {
        var builder = DataBuilder.Create().With(TestSource.EmployeeIdDependencyKey, _employeeId.Value)
            .With(TestSource.BalanceDependencyKey, balance)
            .With(TestSource.AmountAddedDependencyKey, added);
        return new StoryActivityData(builder.GetData());
    }
    
    private StoryEventBody GetNotificationSentEventData(DateTimeOffset atTime)
    {
        var builder = DataBuilder.Create().With(TestSource.AtTimeResultKey, atTime);

        return new StoryEventBody(builder.GetData());
    }
    
    private void EnsureStoryStartedEvent(JourneyStoryStartedEvent journeyStoryChangedEvent, JourneyStoryId journeyStoryId)
    {
        journeyStoryChangedEvent.Should().NotBeNull();
        journeyStoryChangedEvent.StoryId.Should().Be(journeyStoryId);
        journeyStoryChangedEvent.ActivityId.Should().Be(_teamImportActivityId);
        journeyStoryChangedEvent.ActivityName.Should().Be(_teamImportActivityName);
        journeyStoryChangedEvent.EventName.Should().Be(_employeeAddedEventName);
        journeyStoryChangedEvent.EventType.Should().Be(JourneyActivityEventType.Source);
        journeyStoryChangedEvent.EventBody.Should().NotBeNull();
        journeyStoryChangedEvent.EventBody!.Data.Should().ContainKey(TestSource.TeamIdResultKey);
        journeyStoryChangedEvent.EventBody.Data.Should().ContainKey(TestSource.EmployeeIdResultKey);
        journeyStoryChangedEvent.Version.Should().Be(TestSource.Version2);
    }

    private void EnsurePaidHolidayAccrualStartingEvent(JourneyStoryActivityStartingEvent journeyStoryChangedEvent, JourneyStoryId journeyStoryId)
    {
        journeyStoryChangedEvent.Should().NotBeNull();
        journeyStoryChangedEvent.StoryId.Should().Be(journeyStoryId);
        journeyStoryChangedEvent.ActivityId.Should().Be(_paidHolidayAccrualActivityId);
        journeyStoryChangedEvent.ActivityName.Should().Be(_paidHolidayAccrualActivityName);
        journeyStoryChangedEvent.ActivityData.Should().NotBeNull();
        journeyStoryChangedEvent.ActivityData.Data.Should().ContainKey(TestSource.EmployeeIdResultKey);
        journeyStoryChangedEvent.Version.Should().Be(TestSource.Version3);
    }

    private void EnsurePaidHolidayAccrualStartedEvent(JourneyStoryActivityStartedEvent journeyStoryChangedEvent, JourneyStoryId journeyStoryId)
    {
        journeyStoryChangedEvent.Should().NotBeNull();
        journeyStoryChangedEvent.StoryId.Should().Be(journeyStoryId);
        journeyStoryChangedEvent.ActivityId.Should().Be(_paidHolidayAccrualActivityId);
        journeyStoryChangedEvent.ActivityName.Should().Be(_paidHolidayAccrualActivityName);
        journeyStoryChangedEvent.EventName.Should().Be(JourneyActivityEventName.ActivityStarted);
        journeyStoryChangedEvent.Body.Should().BeNull();
        journeyStoryChangedEvent.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        journeyStoryChangedEvent.Version.Should().Be(TestSource.Version4);
    }
    
    private void EnsurePaidHolidayAccrualCompletedEvent(JourneyStoryActivityCompletedEvent journeyStoryChangedEvent, JourneyStoryId journeyStoryId)
    {
        journeyStoryChangedEvent.Should().NotBeNull();
        journeyStoryChangedEvent.StoryId.Should().Be(journeyStoryId);
        journeyStoryChangedEvent.ActivityId.Should().Be(_paidHolidayAccrualActivityId);
        journeyStoryChangedEvent.ActivityName.Should().Be(_paidHolidayAccrualActivityName);
        journeyStoryChangedEvent.EventName.Should().Be(_paidHolidayAccruedEventName);
        journeyStoryChangedEvent.Body.Should().NotBeNull();
        journeyStoryChangedEvent.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        journeyStoryChangedEvent.Version.Should().Be(TestSource.Version5);
    }
    
    private void EnsureNotifyEmployeeStartingEvent(JourneyStoryActivityStartingEvent journeyStoryChangedEvent, JourneyStoryId journeyStoryId)
    {
        journeyStoryChangedEvent.Should().NotBeNull();
        journeyStoryChangedEvent.StoryId.Should().Be(journeyStoryId);
        journeyStoryChangedEvent.ActivityId.Should().Be(_notifyEmployeeActivityId);
        journeyStoryChangedEvent.ActivityName.Should().Be(_notifyEmployeeActivityName);
        journeyStoryChangedEvent.ActivityData.Should().NotBeNull();
        journeyStoryChangedEvent.ActivityData.Data.Should().ContainKey(TestSource.BalanceResultKey);
        journeyStoryChangedEvent.ActivityData.Data.Should().ContainKey(TestSource.AmountAddedResultKey);
        journeyStoryChangedEvent.Version.Should().Be(TestSource.Version6);
    }
    
    private void EnsureNotifyEmployeeStartedEvent(JourneyStoryActivityStartedEvent journeyStoryChangedEvent, JourneyStoryId journeyStoryId)
    {
        journeyStoryChangedEvent.Should().NotBeNull();
        journeyStoryChangedEvent.StoryId.Should().Be(journeyStoryId);
        journeyStoryChangedEvent.ActivityId.Should().Be(_notifyEmployeeActivityId);
        journeyStoryChangedEvent.ActivityName.Should().Be(_notifyEmployeeActivityName);
        journeyStoryChangedEvent.EventName.Should().Be(JourneyActivityEventName.ActivityStarted);
        journeyStoryChangedEvent.Body.Should().BeNull();
        journeyStoryChangedEvent.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        journeyStoryChangedEvent.Version.Should().Be(TestSource.Version7);
    }
    
    private void EnsureNotifyEmployeeSentEvent(JourneyStoryActivityCompletedEvent notifyEmployeeSentEvent, JourneyStoryId journeyStoryId)
    {
        notifyEmployeeSentEvent.Should().NotBeNull();
        notifyEmployeeSentEvent.StoryId.Should().Be(journeyStoryId);
        notifyEmployeeSentEvent.ActivityId.Should().Be(_notifyEmployeeActivityId);
        notifyEmployeeSentEvent.ActivityName.Should().Be(_notifyEmployeeActivityName);
        notifyEmployeeSentEvent.EventName.Should().Be(_notificationSentEventName);
        notifyEmployeeSentEvent.Body.Should().NotBeNull();
        notifyEmployeeSentEvent.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        notifyEmployeeSentEvent.Version.Should().Be(TestSource.Version8);
    }
    
    private void EnsureStoryCompletedEvent(JourneyStoryCompletedEvent journeyStoryChangedEvent, JourneyStoryId journeyStoryId)
    {
        journeyStoryChangedEvent.Should().NotBeNull();
        journeyStoryChangedEvent.StoryId.Should().Be(journeyStoryId);
        journeyStoryChangedEvent.ActivityId.Should().Be(_endOfJourneyActivityId);
        journeyStoryChangedEvent.ActivityName.Should().Be(JourneyActivityName.EndOfJourney);
        journeyStoryChangedEvent.EventName.Should().Be(JourneyActivityEventName.ActivityCompleted);
        journeyStoryChangedEvent.EventType.Should().Be(JourneyActivityEventType.Exit);
        journeyStoryChangedEvent.Body.Should().BeNull();
        journeyStoryChangedEvent.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        journeyStoryChangedEvent.Version.Should().Be(TestSource.Version9);
    }

    private void EnsureStartingActivity(JourneyStoryActivity? activity)
    {
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(JourneyStoryActivityStatus.Starting);
    }
    
    private void EnsureFinishedActivity(JourneyStoryActivity? activity)
    {
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(JourneyStoryActivityStatus.Finished);
    }

    private void EnsureStartedActivity(JourneyStoryActivity? activity)
    {
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(JourneyStoryActivityStatus.Started);
    }
    
    private JourneyStoryEventContext BuildEventContext(JourneyStoryEvent storyEvent)
    {
        var activityId = storyEvent.Id;
        _ = _activitiesMap.TryGetValue(activityId, out var journeyId);
        _ = _journeys.TryGetValue(journeyId, out var journey);
        var activity = journey!.Activities.Single(v => v.Id == activityId);
        var activityEvent = activity.Events.Single(v => v.Name == storyEvent.Name);
        var activityName = activity.Name;
        var eventType = activityEvent.Type;
        var nextActivityId = activityEvent.NextActivityId;
        var nextActivity = journey.Activities.SingleOrDefault(v => v.Id == nextActivityId);
        JourneyActivityTemplate? nextActivityTemplate = null;
        if (nextActivity != null)
        {
            _ = _activityTemplatesMap.TryGetValue(nextActivity.Name, out nextActivityTemplate);
        }
        var nextActivityDependencies = nextActivityTemplate?.Dependencies;

        var storyEventContext = new JourneyStoryEventContext(activityName, eventType, nextActivityId, nextActivity?.Name, nextActivityDependencies);
        return storyEventContext;
    }

    private JourneyStoryState BuildState(JourneyActivityId activityId)
    {
        _ = _activitiesMap.TryGetValue(activityId, out var journeyId);
        _ = _journeys.TryGetValue(journeyId, out var journey);

        var activities = journey!.Activities.Select(v => new JourneyStoryActivity(v.Id, v.Name, JourneyStoryActivityStatus.Ready))
            .ToDictionary(v => v.Id, v => v);

        var data = journey.InitializationData != null
            ? journey.InitializationData!.Data.ToDictionary(v => JourneyStoryDataKey.Create(v.Key), v => v.Value)
            : new Dictionary<JourneyStoryDataKey, JsonElement>();

        return JourneyStoryState.Create(journeyId, activities, data);
    }

    private JourneyActivity BuildTeamImportActivity()
    {
        var teamImportActivityEvents = BuildTeamImportActivityEvents();
        var result = JourneyActivity.Create(_teamImportActivityId, _teamImportActivityName, JourneyActivityStatus.Draft, teamImportActivityEvents, JourneyActivityTtl.Unset);
        return result.GetValueOrDefault();

        JourneyActivityEvent[] BuildTeamImportActivityEvents() =>
        [
            new(_employeeAddedEventName, JourneyActivityEventType.Source, _paidHolidayAccrualActivityId),
            new(_teamImportFailedEventName, JourneyActivityEventType.Fail, _endOfJourneyActivityId)
        ];
    }

    private JourneyActivity BuildPaidHolidayAccrualActivity()
    {
        var paidHolidayAccrualActivityEvents = BuildPaidHolidayAccrualActivityEvents();
        var result = JourneyActivity.Create(_paidHolidayAccrualActivityId, _paidHolidayAccrualActivityName, JourneyActivityStatus.Draft,
            paidHolidayAccrualActivityEvents, JourneyActivityTtl.Unset);
        return result.GetValueOrDefault();

        JourneyActivityEvent[] BuildPaidHolidayAccrualActivityEvents() =>
        [
            JourneyActivityEvent.ActivityStarted,
            new (_paidHolidayAccruedEventName, JourneyActivityEventType.Success, _notifyEmployeeActivityId),
            new (_paidHolidayAccrualFailedEventName, JourneyActivityEventType.Fail, _endOfJourneyActivityId)
        ];
    }

    private JourneyActivity BuildNotifyEmployeeActivity()
    {
        var notifyEmployeeActivityEvents = BuildNotifyEmployeeActivityEvents();
        var result = JourneyActivity.Create(_notifyEmployeeActivityId, _notifyEmployeeActivityName, JourneyActivityStatus.Draft,
            notifyEmployeeActivityEvents, JourneyActivityTtl.Unset);
        return result.GetValueOrDefault();
        
        JourneyActivityEvent[] BuildNotifyEmployeeActivityEvents() =>
        [
            JourneyActivityEvent.ActivityStarted,
            new (_notificationSentEventName, JourneyActivityEventType.Success, _endOfJourneyActivityId),
            new (_notificationFailedEventName, JourneyActivityEventType.Fail, _endOfJourneyActivityId)
        ];
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

    private (JourneyStoryStartedEvent storyStartedEvent, JourneyStoryActivityStartingEvent paidHolidayAccrualStartingEvent, JourneyStoryActivityStartedEvent paidHolidayAccrualStartedEvent, JourneyStoryActivityCompletedEvent paidHolidayAccrualCompletedEvent, JourneyStoryActivityStartingEvent notifyEmployeeStartingEvent, JourneyStoryActivityStartedEvent notifyEmployeeStartedEvent, JourneyStoryActivityCompletedEvent notifyEmployeeSentEvent) GetArrangePositiveFlowEvents(JourneyStoryId journeyStoryId, DateTimeOffset atTime)
    {
        var domainEventVersion = DomainVersion.New;
        
        var storyStartedEventData = GetStoryStartedEventData();
        var paidHolidayAccrualStartingActivityData = GetPaidHolidayAccrualStartingActivityData();
        var (paidHolidayAccruedEventData, balance, added) = GetPaidHolidayAccruedEventData();
        var notifyEmployeeStartingEventData = GetNotifyEmployeeStartingEventData(balance, added);
        var notifyEmployeeSentEventData = GetNotificationSentEventData(atTime);
        
        var storyStartedEvent = new JourneyStoryStartedEvent(journeyStoryId, _teamImportActivityId, _teamImportActivityName, _employeeAddedEventName, JourneyActivityEventType.Source, storyStartedEventData, atTime, ++domainEventVersion);
        var paidHolidayAccrualStartingEvent = new JourneyStoryActivityStartingEvent(journeyStoryId, _paidHolidayAccrualActivityId, _paidHolidayAccrualActivityName, JourneyActivityEventType.Flow,  paidHolidayAccrualStartingActivityData, atTime, ++domainEventVersion);
        var paidHolidayAccrualStartedEvent = new JourneyStoryActivityStartedEvent(journeyStoryId, _paidHolidayAccrualActivityId, _paidHolidayAccrualActivityName, JourneyActivityEventName.ActivityStarted, JourneyActivityEventType.Flow, StoryEventBody.Unset, atTime, ++domainEventVersion);
        var paidHolidayAccrualCompletedEvent = new JourneyStoryActivityCompletedEvent(journeyStoryId, _paidHolidayAccrualActivityId, _paidHolidayAccrualActivityName, _paidHolidayAccruedEventName, JourneyActivityEventType.Success, paidHolidayAccruedEventData, atTime, ++domainEventVersion);
        var notifyEmployeeStartingEvent = new JourneyStoryActivityStartingEvent(journeyStoryId, _notifyEmployeeActivityId, _notifyEmployeeActivityName, JourneyActivityEventType.Flow, notifyEmployeeStartingEventData, atTime, ++domainEventVersion);
        var notifyEmployeeStartedEvent = new JourneyStoryActivityStartedEvent(journeyStoryId, _notifyEmployeeActivityId, _notifyEmployeeActivityName, JourneyActivityEventName.ActivityStarted, JourneyActivityEventType.Flow, StoryEventBody.Unset, atTime, ++domainEventVersion);
        var notifyEmployeeSentEvent = new JourneyStoryActivityCompletedEvent(journeyStoryId, _notifyEmployeeActivityId, _notifyEmployeeActivityName, _notificationSentEventName, JourneyActivityEventType.Fail, notifyEmployeeSentEventData, atTime, ++domainEventVersion );
        var storyCompletedEvent = new JourneyStoryCompletedEvent(journeyStoryId, _endOfJourneyActivityId, JourneyActivityName.EndOfJourney, JourneyActivityEventName.ActivityCompleted, JourneyActivityEventType.Fail, StoryEventBody.Unset, atTime, ++domainEventVersion);
        
        return new (
            storyStartedEvent, 
            paidHolidayAccrualStartingEvent,
            paidHolidayAccrualStartedEvent,
            paidHolidayAccrualCompletedEvent, 
            notifyEmployeeStartingEvent, 
            notifyEmployeeStartedEvent, 
            notifyEmployeeSentEvent);
    }

    private sealed class DataBuilder(Dictionary<string, JsonElement> data)
    {
        public static DataBuilder Create(string key, JsonElement rawData)
        {
            var data = new Dictionary<string, JsonElement>() { { key, rawData } };
            return new DataBuilder(data);
        }
        
        public static DataBuilder Create()
        {
            var data = new Dictionary<string, JsonElement>();
            return new DataBuilder(data);
        }
        
        public DataBuilder With<TValue>(string key, TValue value)
        {
            data[key] = JsonSerializer.SerializeToElement(value); 
            return this;
        }

        public IReadOnlyDictionary<string, JsonElement> GetData() => data;
    }
}