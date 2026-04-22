using Odyssey.HRMS.EventLogLite.Base;
using System.Runtime.InteropServices.ObjectiveC;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Odyssey.HRMS.EventLogLite.Entities;

public sealed record LogMessage<TEvent> where TEvent : class
{
    [JsonConverter(typeof(Int32CustomConverter))]
    public int RecordLength { get; set; }
    
    [JsonConverter(typeof(Int32CustomConverter))]
    public int KeyLength { get; set; }
    public string Key { get; set; }
    
    [JsonConverter(typeof(Int32CustomConverter))]
    public int PayloadLength { get; set; }
    public TEvent Payload { get; set; }
    
    [JsonConverter(typeof(Int32CustomConverter))]
    public int MetadataLength { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    
    [JsonConverter(typeof(Int64CustomConverter))]
    public long Timestamp { get; set; } // => Timestamp delta
    /// <summary>
    /// Unique number inside partition 
    /// </summary>
    [JsonConverter(typeof(Int64CustomConverter))]
    public long Offset { get; set; } // => Offset delta
    
    public static LogMessage<TEvent> Create(LogRequest<TEvent> request, long offset)
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
    
    public static LogMessage<TEvent> Create(string key, TEvent payload,  long offset)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new LogMessage<TEvent>
        {
            Key = key,
            Payload = payload,
            Timestamp = timestamp,
            Offset = offset,
            Metadata = new Dictionary<string, object>()
        };
    }
    
    public static LogMessage<TEvent> CreateForJson(LogRequest<TEvent> request, long offset)
    {
        var serializerOptions = JsonFileEventLogger.SerializerOptions;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var msg = new  LogMessage<TEvent>
        {
            Key = request.Key!,
            Payload = request.Payload,
            Metadata = request.Metadata,
            Timestamp = timestamp,
            Offset = offset,
            RecordLength = 0,
            KeyLength = 0,
            PayloadLength = 0,
            MetadataLength = 0,
        };

        var buffer = JsonSerializer.SerializeToUtf8Bytes(msg, serializerOptions);
        msg.RecordLength = buffer.Length;
        msg.MetadataLength = GetJsonValueLength(buffer, nameof(Metadata));
        msg.KeyLength = GetJsonValueLength(buffer, nameof(Key));
        msg.PayloadLength = GetJsonValueLength(buffer, nameof(Payload));

        return msg;
    }
    
    private static int GetJsonValueLength(ReadOnlySpan<byte> jsonBuffer, string keyName)
    {
        const int defaultResult = 0;
        var reader = new Utf8JsonReader(jsonBuffer);
        
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals(keyName))
            {
                if (!reader.Read()) return defaultResult;

                var start = reader.TokenStartIndex;

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        reader.Skip();
                        return (int)(reader.BytesConsumed - start);
                    case JsonTokenType.String:
                        return (int)(reader.BytesConsumed - start);
                    default:
                        return (int)(reader.BytesConsumed - start);
                }
            }
        }
        
        return defaultResult;
    }
}

public sealed record PollRequest
{
    // public int BatchSize { get; init; }
    // public string TopicName { get; init; } = null!;
    // public string GroupName { get; init; } = null!;
    // public Guid RequestId { get; init; }
    // public DateTimeOffset OccuredAt { get; init; }
    public long StartPosition { get; set; }
    
}

public sealed class BatchPoolRequest
{
    public string TopicName { get; init; } = null!;
    public string GroupName { get; init; } = null!;
    public string ConsumerId { get; init; } = null!;
    public int ConsumerGenerationId { get; init; }
    public long Offset { get; init; }
    public byte PartitionId { get; init; }
    public int MaxBytes { get; init; }
    public int MaxWaitTimeMs { get; init; }
    public Guid RequestId { get; init; }
    public DateTimeOffset OccuredAt { get; init; }
}

public sealed class BatchPoolResponse<TEvent> where TEvent : class
{
    public string TopicName { get; init; } = null!;
    public IReadOnlyCollection<LogResponse<TEvent>> Items { get; init; } = Array.Empty<LogResponse<TEvent>>();
    
    public Guid ResponseId { get; init; }
    
    public BatchPoolResponseError? Error { get; init; } 
}

public sealed record BatchPoolResponseError(string ErrorMessage, string ErrorCode)
{
    private const string UnknownPartitionKey = "UNKNOWN_TOPIC_OR_PARTITION";

    public static BatchPoolResponseError UnknownPartition(PartitionKey partitionKey)
        => new ($"Partition {partitionKey.TopicName}-{partitionKey.PartitionId} doesn't exist", UnknownPartitionKey);
}

public sealed class StreamPoolRequest
{
    public string TopicName { get; init; } = null!;
    public string GroupName { get; init; } = null!;
    public Guid RequestId { get; init; }
    public DateTimeOffset OccuredAt { get; init; }
    public long Offset { get; init; }
    public byte ConsumerId { get; init; }
}


public sealed class LogOffsetRequest
{
    public LogOffsetKey Key { get; init; } = null!;
    public LogOffsetValue Value { get; init; } =  null!;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public Guid RequestId { get; init; }
}

public sealed record LogOffsetKey(string ConsumerGroupName, string TopicName, byte PartitionId)
{
    public override string ToString()
    {
        return $"{ConsumerGroupName}.{TopicName}.{PartitionId}";
    }
}

public sealed record LogOffsetValue(long NextMsgOffset, long CommitTimestamp)
{
    public static LogOffsetValue New => new LogOffsetValue(0, 0);
    public long Offset => NextMsgOffset - 1;
}


public sealed class ReadOffsetRequest
{
    public LogOffsetKey Key { get; init; } = null!;
    public Guid RequestId { get; init; }
    public DateTimeOffset OccuredAt { get; init; }
}

public sealed class LogOffsetMessage
{
    public LogOffsetKey Key { get; init; } = null!;
    public LogOffsetValue Value { get; init; } =  null!;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTimeOffset? OccuredAt { get; init; }
    
    public static LogOffsetMessage CreateNew(LogOffsetKey key) => new LogOffsetMessage() { Key = key, Value = LogOffsetValue.New };
}