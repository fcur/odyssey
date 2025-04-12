namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record JourneyId(Guid Value)
{
    public static JourneyId New()
    {
        var timeNow = DateTimeOffset.UtcNow;
        var id = Guid.CreateVersion7(timeNow);
        var journeyId = new JourneyId(id);

        return journeyId;
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