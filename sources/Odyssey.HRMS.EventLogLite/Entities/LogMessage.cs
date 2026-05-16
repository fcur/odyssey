using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Serializer;
using System.Runtime.InteropServices.ObjectiveC;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Odyssey.HRMS.EventLogLite.Entities;

public record LogMessage
{
    [JsonConverter(typeof(Int32JsonConverter))]
    public int RecordLength { get; set; }
    [JsonConverter(typeof(Int32JsonConverter))]
    public int KeyLength { get; set; }
    [JsonConverter(typeof(Int32JsonConverter))]
    public int PayloadLength { get; set; }
    [JsonConverter(typeof(Int32JsonConverter))]
    public int MetadataLength { get; set; }
    
    [JsonConverter(typeof(Int64JsonConverter))]
    public long Timestamp { get; set; } // => Timestamp delta
    /// <summary>
    /// Unique number inside partition 
    /// </summary>
    [JsonConverter(typeof(Int64JsonConverter))]
    public long Offset { get; set; } // => Offset delta
    public Dictionary<string, object>? Metadata { get; set; }
}

public sealed record LogMessage<TEvent> : LogMessage where TEvent : class
{
    public string Key { get; set; }
    public TEvent Payload { get; set; }
    
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
        var serializerOptions = LogSerializer.JsonOptions;
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


// public sealed class LogOffsetRecordBatch
// {
//     public long BaseOffset{ get; init; }
//     public long LastOffsetDelta{ get; init; }
//     public LogOffsetRecordBatchItem[] Items { get; init; } =[];
// }
//
// public sealed class LogOffsetRecordBatchItem
// {
//     // key
//     public string Topic { get; init; } = null!;
//     public string GroupId { get; init; } = null!;
//     public byte PartitionId { get; init; }
//     
//     // value
//     public long OffsetDelta { get; init; }
//     public long CommitTimestamp { get; init; }
//     
//     // metadata
//     public Dictionary<string, object> Metadata { get; init; } = new();
// }

public sealed record LogCommitKey(string Topic, string GroupId, byte PartitionId);

public sealed record LogCommitValue(long Offset, long Timestamp);



public record LogMessageBatch
{
    /// <summary>
    /// Base offset for whole batch. Equals to the 1st item's offset.
    /// </summary>
    [JsonConverter(typeof(Int64JsonConverter))]
    public long BaseOffset{ get; init; }
    
    /// <summary>
    /// Total batch size in bytes.
    /// </summary>
    [JsonConverter(typeof(Int32JsonConverter))]
    public int BatchLength { get; set; }
    
    [JsonConverter(typeof(ByteJsonConverter))]
    public byte Version { get; set; }
    
    [JsonConverter(typeof(Int32JsonConverter))]
    public int Checksum { get; set; }
    
    [JsonConverter(typeof(ByteJsonConverter))]
    public byte Attributes { get; set; }
    
    /// <summary>
    /// Relative offset of the last item in batch.
    /// </summary>
    [JsonConverter(typeof(Int64JsonConverter))]
    public long LastOffsetDelta{ get; init; }
    
    [JsonConverter(typeof(Int64JsonConverter))]
    public long FirstTimestamp { get; set; }
    
    [JsonConverter(typeof(Int64JsonConverter))]
    public long MaxTimestamp { get; set; }
    
    /// <summary>
    /// Unique producer ID for idempotence.
    /// </summary>
    [JsonConverter(typeof(Int64JsonConverter))]
    public long ProducerId { get; set; }
    
    [JsonConverter(typeof(ByteJsonConverter))]
    public byte ProducerEpoch { get; set; }
    
    [JsonConverter(typeof(Int32JsonConverter))]
    public int BatchOrder { get; set; }
    
    /// <summary>
    /// Compaction might delete old records.
    /// </summary>
    [JsonConverter(typeof(Int32JsonConverter))]
    public int ItemsCount { get; set; }
}

public sealed record LogMessageBatch<TKey, TData>: LogMessageBatch where TKey : class where TData: class
{
    /// <summary>
    /// Batch items.
    /// </summary>
    public LogMessageBatchItem<TKey, TData>[] Items { get; init; } = [];
}


public readonly record struct MessageItemLength(int RecordPosition, int RecordLength, int KeyPosition, int KeyLength, int PayloadPosition, int PayloadLength, int MetadataPosition, int MetadataLength);

public record LogMessageBatchItem
{
    /// <summary>
    /// Batch item size in bytes.
    /// </summary>
    [JsonConverter(typeof(Int32JsonConverter))]
    public int RecordLength { get; set; }
    
    [JsonConverter(typeof(ByteJsonConverter))]
    public byte Attributes { get; set; }
    
    [JsonConverter(typeof(Int64JsonConverter))]
    public long OffsetDelta { get; set; }
    
    [JsonConverter(typeof(Int64JsonConverter))]
    public long TimestampDelta { get; set; }
    
    /// <summary>
    /// Key size in bytes.
    /// </summary>
    [JsonConverter(typeof(Int32JsonConverter))]
    public int KeyLength { get; set; }
    /// <summary>
    /// Payload size in bytes.
    /// </summary>
    [JsonConverter(typeof(Int32JsonConverter))]
    public int PayloadLength { get; set; }
    
    [JsonConverter(typeof(Int32JsonConverter))]
    public int MetadataLength { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public sealed record LogMessageBatchItem<TKey, TData>: LogMessageBatchItem  where TKey : class where TData: class
{
    /// <summary>
    /// Key data.
    /// </summary>
    public TKey Key { get; init; } = null!;
    
    /// <summary>
    /// Payload data.
    /// </summary>
    public TData Payload { get; set; } = null!;
}
