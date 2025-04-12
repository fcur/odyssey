namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public sealed record JourneyStoryId(Guid Value)
{
    public static JourneyStoryId New()
    {
        var timeNow = DateTimeOffset.UtcNow;
        var id = Guid.CreateVersion7(timeNow);
        var journeyActivationId = new JourneyStoryId(id);

        return journeyActivationId;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Value.ToString("D");
    }
}