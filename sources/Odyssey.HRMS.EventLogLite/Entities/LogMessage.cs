namespace Odyssey.HRMS.EventLogLite.Entities;

public sealed record LogMessage<TEvent> where TEvent : class
{
    public string Key { get; set; }
    public TEvent Payload { get; set; }
    public long Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; }

    /// <summary>
    /// Unique number inside partition 
    /// </summary>
    public ulong Offset { get; set; }

    public static LogMessage<TEvent> Create(LogRequest<TEvent> request, ulong offset)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new LogMessage<TEvent>
        {
            Key = request.Key!,
            Payload = request.Payload,
            Timestamp = timestamp,
            Offset = offset,
            Metadata = request.Metadata
        };
    }
}

public sealed class PollRequest
{
    public int BatchSize { get; init; }
    public string TopicName { get; init; } = null!;
    public string GroupName { get; init; } = null!;
    public Guid RequestId { get; init; }
    public DateTimeOffset OccuredAt { get; init; }
}

public sealed class LogOffsetRequest
{
    public LogOffsetKey Key { get; init; } = null!;
    public LogOffsetValue Value { get; init; } =  null!;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public Guid RequestId { get; init; }
    public DateTimeOffset OccuredAt { get; init; }
}

public sealed record LogOffsetKey(string ConsumerGroupName, string TopicName, byte PartitionId)
{
    public override string ToString()
    {
        return $"{ConsumerGroupName}.{TopicName}.{PartitionId}";
    }
}

public sealed record LogOffsetValue(ulong NextMsgOffset, long CommitTimestamp)
{
    public static LogOffsetValue New => new LogOffsetValue(0, 0);
}