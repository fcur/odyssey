using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public sealed record JourneyStoryActivity(JourneyActivityId Id, JourneyActivityName ActivityName, JourneyStoryActivityStatus Status) 
    : NestedDomainEntity<JourneyActivityId>(Id)
{
    public JourneyStoryActivityStatus Status { get; private set; } = Status;
    
    public void SetStarted()
    {
        Status = JourneyStoryActivityStatus.Started;
    }

    public void Start()
    {
        Status = JourneyStoryActivityStatus.Starting;
    }
    
    public void SetFinished()
    {
        Status = JourneyStoryActivityStatus.Finished;
    }

    public void SetCancelled()
    {
        Status = JourneyStoryActivityStatus.Cancelled;
    }
    
    public bool IsStarted => Status == JourneyStoryActivityStatus.Started;
    public bool IsFinished => Status is JourneyStoryActivityStatus.Finished or JourneyStoryActivityStatus.Cancelled;

    public bool CouldBeStarted => Status is JourneyStoryActivityStatus.Starting;

    public bool CouldBeFinished => Status is JourneyStoryActivityStatus.Started;

    public bool CouldBeCancelled => Status is JourneyStoryActivityStatus.Starting 
        or JourneyStoryActivityStatus.Started 
        or JourneyStoryActivityStatus.Cancellation;
}

public enum JourneyStoryActivityStatus: byte
{
    Ready,
    Starting,
    Started,
    Cancellation,
    Cancelled,
    Finished
}