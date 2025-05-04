namespace Odyssey.HRMS.EventLogLite.Entities;

public sealed class LogRequest<TEvent> where TEvent : class
{
    public string Key { get; init; }
    public TEvent Payload { get; init; }
    public int PartitionId { get; init; } = 0;
}

public sealed class LogRespone<TEvent> where TEvent : class
{
    public string Key { get; init; }
    public TEvent Payload { get; init; }
    public long Offset { get; init; }
    public int PartitionId { get; init; } = 0;
    public DateTimeOffset Timestamp { get; init; }
}