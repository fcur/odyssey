namespace Odyssey.HRMS.EventLogLite;

public sealed class TestEvent
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string SourceContext { get; set; }
}