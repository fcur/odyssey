namespace Odyssey.HRMS.Domain.JourneyEntity;

public record struct JourneyStatus(byte Value, string Name)
{
    public static readonly JourneyStatus Draft = new JourneyStatus(0, nameof(Draft));
    public static readonly JourneyStatus Ready = new JourneyStatus(1, nameof(Ready));
    public static readonly JourneyStatus Started = new JourneyStatus(2, nameof(Started));
    public static readonly JourneyStatus Finished = new JourneyStatus(3, nameof(Finished));
    public static readonly JourneyStatus Deleted = new JourneyStatus(byte.MaxValue, nameof(Deleted));
}