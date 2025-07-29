namespace Odyssey.HRMS.EventLogLite.Entities;

public sealed class LogRequest<TEvent> where TEvent : class
{
    public string? Key { get; init; }
    public TEvent Payload { get; init; } = null!;
    public byte? PartitionId { get; init; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class LogResponse<TEvent> where TEvent : class
{
    public string? Key { get; init; }
    public TEvent Payload { get; init; } = null!;
    public long Offset { get; init; }
    public byte PartitionId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
