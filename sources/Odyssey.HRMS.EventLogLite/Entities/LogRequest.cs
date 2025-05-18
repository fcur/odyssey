namespace Odyssey.HRMS.EventLogLite.Entities;

public sealed class LogRequest<TEvent> where TEvent : class
{
    public string? Key { get; init; }
    public TEvent Payload { get; init; }
    public byte? PartitionId { get; init; }
}

public sealed class LogRespone<TEvent> where TEvent : class
{
    public string? Key { get; init; }
    public TEvent Payload { get; init; }
    public ulong Offset { get; init; }
    public byte PartitionId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}