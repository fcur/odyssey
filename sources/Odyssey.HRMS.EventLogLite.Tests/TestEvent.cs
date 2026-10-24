namespace Odyssey.HRMS.EventLogLite.Tests;

public sealed record TestEvent
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string Message { get; init; }
    public bool Skipped { get; init; }
        
    // ReSharper disable once ConvertConstructorToMemberInitializers
    public TestEvent()
    {
        Id = Guid.Empty;
        OccurredAt = DateTimeOffset.MinValue;
        Skipped = true;
        Message = string.Empty;
    }
}