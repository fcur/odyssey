namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

public record struct JourneyActivityStatus(byte Value, string Name)
{
    public static readonly JourneyActivityStatus Draft = new JourneyActivityStatus(0, nameof(Draft));
    public static readonly JourneyActivityStatus Ready = new JourneyActivityStatus(1, nameof(Ready));
    public static readonly JourneyActivityStatus Started = new JourneyActivityStatus(2, nameof(Started));
    public static readonly JourneyActivityStatus Finished = new JourneyActivityStatus(3, nameof(Finished));
    public static readonly JourneyActivityStatus Deleted = new JourneyActivityStatus(byte.MaxValue, nameof(Deleted));
}