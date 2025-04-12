using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.EmployeeEntity;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;
using System.Text.Json;

namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public sealed class JourneyStoryState : AggregateRootState<JourneyStoryChangedEvent>
{
    private readonly JourneyStoryData _data;
    private readonly Dictionary<JourneyActivityId, JourneyStoryActivity> _activities;

    public EmployeeId EmployeeId { get; private set; }
    public JourneyId JourneyId { get; init; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public bool IsFinished => FinishedAt.HasValue;

    private JourneyStoryState(JourneyId journeyId, Dictionary<JourneyActivityId, JourneyStoryActivity> activities,
        Dictionary<JourneyStoryDataKey, JsonElement> data)
    {
        _activities = activities;
        _data = new JourneyStoryData(data);
        JourneyId = journeyId;
    }

    public static JourneyStoryState Create(JourneyId journeyId, Dictionary<JourneyActivityId, JourneyStoryActivity> activities)
        => Create(journeyId, activities, new Dictionary<JourneyStoryDataKey, JsonElement>());

    public static JourneyStoryState Create(JourneyId journeyId, Dictionary<JourneyActivityId, JourneyStoryActivity> activities,
        Dictionary<JourneyStoryDataKey, JsonElement> data)
        => new JourneyStoryState(journeyId, activities, data);

    
    protected internal override void Apply(JourneyStoryChangedEvent domainEvent)
    {
        switch (domainEvent)
        {
            case JourneyStoryActivityStartingEvent activityStartingEvent:
                Apply(activityStartingEvent);
                break;
            case JourneyStoryActivityStartedEvent activityStartedEvent:
                Apply(activityStartedEvent);
                break;
            case JourneyStoryStartedEvent storyStartedEvent:
                Apply(storyStartedEvent);
                break;
            case JourneyStoryCompletedEvent storyCompletedEvent:
                Apply(storyCompletedEvent);
                break;
            case JourneyStoryActivityCompletedEvent activityCompletedEvent:
                Apply(activityCompletedEvent);
                break;
            default: throw new NotImplementedException();
        }
    }

    private void Apply(JourneyStoryActivityStartingEvent activityStartingEvent)
    {
        var activityId = activityStartingEvent.ActivityId;
        var activity = GetActivityOrThrowException(activityId);
        
        activity.Start();
    }

    private void Apply(JourneyStoryStartedEvent storyStartedEvent)
    {
        var activityId = storyStartedEvent.ActivityId;
        var activity = GetActivityOrThrowException(activityId);

        activity.SetFinished();

        _data.EnrichWithEventResponse(storyStartedEvent.ActivityName, storyStartedEvent.EventName, storyStartedEvent.EventBody);

        if (!GetData<Guid>("EmployeeId", storyStartedEvent.ActivityName, storyStartedEvent.EventName).TryGetValue(out var employeeIdResult))
        {
            throw new ArgumentException("EmployeeId");
        }

        StartedAt = storyStartedEvent.CreatedAt;
        EmployeeId = new EmployeeId(employeeIdResult);
    }

    private JourneyStoryActivity GetActivityOrThrowException(JourneyActivityId activityId)
    {
        if (!_activities.TryGetValue(activityId, out var activity))
        {
            throw new ArgumentOutOfRangeException(activityId.ToString());
        }

        return activity;
    }

    public Result<JourneyStoryActivity> GetActivity(JourneyActivityId activityId)
    {
        if (!_activities.TryGetValue(activityId, out var activity))
        {
            return Result.Failure<JourneyStoryActivity>($"Activity {activityId} does not exist");
        }

        return activity;
    }

    private void Apply(JourneyStoryCompletedEvent storyCompletedEvent)
    {
        var activityId = storyCompletedEvent.ActivityId;
        var activity = GetActivityOrThrowException(activityId);
        activity.SetFinished();
        _data.EnrichWithEventResponse(storyCompletedEvent.ActivityName, storyCompletedEvent.EventName, storyCompletedEvent.Body);

        var unwantedActivities = _activities.Values.Where(v => v.Status != JourneyStoryActivityStatus.Finished);
        // TODO: investigate notification for canceled activities
        foreach (var unwantedActivity in unwantedActivities)
        {
            activity = GetActivityOrThrowException(unwantedActivity.Id);
            activity.SetCancelled();
        }

        FinishedAt = storyCompletedEvent.CreatedAt;
    }

    private void Apply(JourneyStoryActivityCompletedEvent activityCompletedEvent)
    {
        var activityId = activityCompletedEvent.ActivityId;
        var activity = GetActivityOrThrowException(activityId);

        activity.SetFinished();

        _data.EnrichWithEventResponse(activityCompletedEvent.ActivityName, activityCompletedEvent.EventName, activityCompletedEvent.Body);
    }
    
    private void Apply(JourneyStoryActivityStartedEvent activityStartedEvent)
    {
        var activityId = activityStartedEvent.ActivityId;
        var activity = GetActivityOrThrowException(activityId);
        activity.SetStarted();

        _data.EnrichWithEventResponse(activityStartedEvent.ActivityName, activityStartedEvent.EventName, activityStartedEvent.Body);
    }

    public Result<T> GetData<T>(string key, JourneyActivityName? activityName = null, JourneyActivityEventName? eventName = null)
    {
        var storyDataKey = new JourneyStoryDataKey(key, activityName, eventName);
        return GetData<T>(storyDataKey);
    }

    private Result<T> GetData<T>(JourneyStoryDataKey key)
    {
        try
        {
            if (!_data.TryGetValue(key, out var data))
            {
                return Result.Failure<T>($"Data is missing for the requested key: '{key}'");
            }

            return data.Deserialize<T>() ?? throw new InvalidOperationException();
        }
        catch (JsonException je)
        {
            return Result.Failure<T>(je.Message);
        }
        catch (NotSupportedException nse)
        {
            return Result.Failure<T>(nse.Message);
        }
    }

    // TODO: refactor
    public Result<Dictionary<string, JsonElement>, JourneyStoryError> GetEventBody(IReadOnlyCollection<JourneyActivityTemplateDependency>? activityDependencies)
        => _data.GetEventBody(activityDependencies);
}