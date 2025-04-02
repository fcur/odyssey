namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

public sealed record JourneyActivityId(Guid Value)
{
    public static JourneyActivityId New()
    {
        var timeNow = DateTimeOffset.UtcNow;
        var id = Guid.CreateVersion7(timeNow);
        var journeyActivityId = new JourneyActivityId(id);

        return journeyActivityId;
    }
    
    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
    
    public override string ToString()
    {
        return Value.ToString("D");
    }

    public static JourneyActivityId? Unset = null;
}