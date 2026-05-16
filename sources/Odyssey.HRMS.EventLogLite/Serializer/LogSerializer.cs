using Odyssey.HRMS.EventLogLite.Entities;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Odyssey.HRMS.EventLogLite.Serializer;

public static class LogSerializer
{
    public const string JsonByteFormat = "D3";
    public const string JsonInt32Format = "D10";
    public const string JsonInt64Format = "D20";
    private const string EmptyRecordLength = "0000000000";
    
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    public static readonly JsonWriterOptions Utf8WriterOptions = new() { Indented = false };
    private static readonly StandardFormat LengthPropertyFormat = new ('D', 10);
    private static readonly int RecordLengthLogMessageOffset;
    private static readonly int BatchLengthLogMessageOffset;
    
    private const byte JsonLineDivider = 10;
    
    static LogSerializer()
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        RecordLengthLogMessageOffset = GetOffsetPosition(buffer, writer =>
        {
            writer.WriteString(nameof(LogMessage.RecordLength), EmptyRecordLength);
        }, span => span.IndexOf("0000000000"u8));
        
        BatchLengthLogMessageOffset = GetOffsetPosition(buffer, writer =>
        {
            writer.WriteString(nameof(LogMessageBatch.BaseOffset), 0.ToString(JsonInt64Format));
            writer.WriteString(nameof(LogMessageBatch.BatchLength), EmptyRecordLength);
        }, span => span.LastIndexOf("0000000000"u8));
        
        buffer.Clear();
    }

    public static int SerializeAsJsonRow<TEvent>(ArrayBufferWriter<byte> sharedBuffer, LogMessage<TEvent> message) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(sharedBuffer);
        ArgumentNullException.ThrowIfNull(message);
        
        var startPosition = sharedBuffer.WrittenCount;
        var keyBytesLength = System.Text.Encoding.UTF8.GetByteCount(message.Key);
        int currentPayloadLengthOffset, payloadLength,  currentMetadataLengthOffset = 0, metadataLength = 0;
        
        using (var writer = new Utf8JsonWriter(sharedBuffer, Utf8WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(message.RecordLength), EmptyRecordLength);
            
            writer.WriteNumber(nameof(message.Timestamp), message.Timestamp);
            writer.WriteNumber(nameof(message.Offset), message.Offset);
            
            writer.WriteNumber(nameof(message.KeyLength), keyBytesLength);
            writer.WriteString(nameof(message.Key), message.Key);
            
            // payload length mark
            writer.WritePropertyName(nameof(message.PayloadLength)); 
            writer.Flush();
            currentPayloadLengthOffset = sharedBuffer.WrittenCount + 1- startPosition;
            writer.WriteStringValue(EmptyRecordLength);
            
            // payload length
            writer.WritePropertyName(nameof(message.Payload)); 
            writer.Flush();
            var payloadStart = sharedBuffer.WrittenCount;
            JsonSerializer.Serialize(writer, message.Payload, JsonOptions);
            payloadLength = sharedBuffer.WrittenCount - payloadStart;

            if (message.Metadata != null)
            {
                // metadata length mark
                writer.WritePropertyName(nameof(message.MetadataLength)); writer.Flush();
                currentMetadataLengthOffset =  sharedBuffer.WrittenCount + 1 - startPosition;
                writer.WriteStringValue(EmptyRecordLength);
                
                // metadata length
                writer.WritePropertyName(nameof(message.Metadata)); writer.Flush();
                var metadataStart = sharedBuffer.WrittenCount;
                JsonSerializer.Serialize(writer, message.Metadata, JsonOptions);
                metadataLength = sharedBuffer.WrittenCount - metadataStart;
            }
            
            writer.WriteEndObject();
            writer.Flush();
        }
        
        // new line
        var span = sharedBuffer.GetSpan(1);
        span[0] = JsonLineDivider;
        sharedBuffer.Advance(1);
        
        var recordLength = sharedBuffer.WrittenCount - startPosition;
        
        var allWrittenSpan = sharedBuffer.WrittenSpan;
        var messageSpan = allWrittenSpan.Slice(startPosition, recordLength);
        var mutableSpan = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(messageSpan), recordLength);

        UpdatePropertyLength(mutableSpan, RecordLengthLogMessageOffset, recordLength);
        UpdatePropertyLength(mutableSpan, currentPayloadLengthOffset, payloadLength);
        if (currentMetadataLengthOffset > 0)
        {
            UpdatePropertyLength(mutableSpan, currentMetadataLengthOffset, metadataLength);
        }

        return recordLength;
    }
    
    public static int SerializeAsJsonRow<TKey, TData>(ArrayBufferWriter<byte> sharedBuffer, LogMessageBatch<TKey, TData> messageBatch) where TKey : class where TData: class
    {
        ArgumentNullException.ThrowIfNull(sharedBuffer);
        ArgumentNullException.ThrowIfNull(messageBatch);
        
        var startPosition = sharedBuffer.WrittenCount;
        var messageItemsLength = new MessageItemLength[messageBatch.ItemsCount];
        int batchLengthPosition, batchLength,
            recordLengthPosition, recordLengthStart, recordLength,
            keyLengthPosition, keyLengthStart, keyLength, 
            payloadLengthPosition, payloadLengthStart, payloadLength,
            metadataLengthPosition, metadataLengthStart, metadataLength;
        
        using (var writer = new Utf8JsonWriter(sharedBuffer, Utf8WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(messageBatch.BaseOffset), messageBatch.BaseOffset.ToString(JsonInt64Format));
            
            // batch length mark
            writer.WritePropertyName(nameof(messageBatch.BatchLength)); writer.Flush();
            batchLengthPosition = sharedBuffer.WrittenCount + 1 - startPosition;
            writer.WriteStringValue(EmptyRecordLength);
            
            writer.WriteString(nameof(messageBatch.Version), messageBatch.Version.ToString(JsonByteFormat));
            writer.WriteString(nameof(messageBatch.Checksum), messageBatch.Checksum.ToString(JsonInt32Format));
            writer.WriteString(nameof(messageBatch.Attributes), messageBatch.Attributes.ToString(JsonByteFormat));
            writer.WriteString(nameof(messageBatch.LastOffsetDelta), messageBatch.LastOffsetDelta.ToString(JsonInt64Format));
            writer.WriteString(nameof(messageBatch.FirstTimestamp), messageBatch.FirstTimestamp.ToString(JsonInt64Format));
            writer.WriteString(nameof(messageBatch.MaxTimestamp), messageBatch.MaxTimestamp.ToString(JsonInt64Format));
            writer.WriteString(nameof(messageBatch.ProducerId), messageBatch.ProducerId.ToString(JsonInt64Format));
            writer.WriteString(nameof(messageBatch.ProducerEpoch), messageBatch.ProducerEpoch.ToString(JsonByteFormat));
            writer.WriteString(nameof(messageBatch.BatchOrder), messageBatch.BatchOrder.ToString(JsonInt32Format));
            writer.WriteString(nameof(messageBatch.ItemsCount), messageBatch.ItemsCount.ToString(JsonInt32Format));

            if (messageBatch.ItemsCount > 0)
            {
                
                writer.WritePropertyName(nameof(messageBatch.Items));
                writer.WriteStartArray();

                for (var i = 0; i < messageBatch.ItemsCount; i++)
                {
                    recordLengthPosition = 0; recordLengthStart = 0;
                    keyLengthPosition = 0; keyLength = 0; keyLengthStart = 0;
                    payloadLengthPosition = 0; payloadLength = 0; payloadLengthStart = 0;
                    metadataLengthPosition = 0; metadataLengthStart = 0; metadataLength = 0;
                    
                    var item = messageBatch.Items[i];
                    
                    writer.Flush();
                    recordLengthStart = sharedBuffer.WrittenCount;
                    
                    writer.WriteStartObject();
                    
                    // record length mark
                    writer.WritePropertyName(nameof(item.RecordLength)); writer.Flush();
                    recordLengthPosition = sharedBuffer.WrittenCount + 1 - startPosition;
                    writer.WriteStringValue(EmptyRecordLength);
                    
                    // writer.WriteString(nameof(item.RecordLength), EmptyRecordLength);
                    
                    writer.WriteString(nameof(item.Attributes), item.Attributes.ToString(JsonByteFormat));
                    writer.WriteString(nameof(item.OffsetDelta), item.OffsetDelta.ToString(JsonInt64Format));
                    writer.WriteString(nameof(item.TimestampDelta), item.TimestampDelta.ToString(JsonInt64Format));
                    
                    // key length mark
                    writer.WritePropertyName(nameof(item.KeyLength)); writer.Flush();
                    keyLengthPosition = sharedBuffer.WrittenCount + 1 - startPosition;
                    writer.WriteStringValue(EmptyRecordLength);
                    // key length
                    writer.WritePropertyName(nameof(item.Key)); 
                    writer.Flush();
                    keyLengthStart = sharedBuffer.WrittenCount;
                    JsonSerializer.Serialize(writer, item.Key, JsonOptions);
                    keyLength = sharedBuffer.WrittenCount - keyLengthStart;
                    
                    // payload length mark
                    writer.WritePropertyName(nameof(item.PayloadLength)); 
                    writer.Flush();
                    payloadLengthPosition = sharedBuffer.WrittenCount + 1- startPosition;
                    writer.WriteStringValue(EmptyRecordLength);
                    // payload length
                    writer.WritePropertyName(nameof(item.Payload)); writer.Flush();
                    payloadLengthStart = sharedBuffer.WrittenCount;
                    JsonSerializer.Serialize(writer, item.Payload, JsonOptions);
                    payloadLength = sharedBuffer.WrittenCount - payloadLengthStart;
                    
                    if (item.Metadata != null)
                    {
                        // metadata length mark
                        writer.WritePropertyName(nameof(item.MetadataLength)); writer.Flush();
                        metadataLengthPosition =  sharedBuffer.WrittenCount + 1 - startPosition;
                        writer.WriteStringValue(EmptyRecordLength);
                
                        // metadata length
                        writer.WritePropertyName(nameof(item.Metadata)); writer.Flush();
                        metadataLengthStart = sharedBuffer.WrittenCount;
                        JsonSerializer.Serialize(writer, item.Metadata, JsonOptions);
                        metadataLength = sharedBuffer.WrittenCount - metadataLengthStart;
                    }
                    
                    
                    writer.WriteEndObject();
                    writer.Flush();
                    
                    recordLength = sharedBuffer.WrittenCount - recordLengthStart;
                    
                    messageItemsLength[i] = new MessageItemLength(
                        recordLengthPosition, recordLength, 
                        keyLengthPosition, keyLength, 
                        payloadLengthPosition,  payloadLength, 
                        metadataLengthPosition, metadataLength);
                }
                
                writer.WriteEndArray();
            }
            
            writer.WriteEndObject();
            writer.Flush();
        }
        
        // new line
        var span = sharedBuffer.GetSpan(1);
        span[0] = JsonLineDivider;
        sharedBuffer.Advance(1);
        
        batchLength = sharedBuffer.WrittenCount - startPosition;
        
        var allWrittenSpan = sharedBuffer.WrittenSpan;
        var messageSpan = allWrittenSpan.Slice(startPosition, batchLength);
        var mutableSpan = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(messageSpan), batchLength);
        
        UpdatePropertyLength(mutableSpan, batchLengthPosition, batchLength);

        
        return batchLength;
    }
    
    /// <summary>
    /// In-place update
    /// </summary>
    private static void UpdatePropertyLength(Span<byte> data, int offset, int value)
    {
        var  destination = data.Slice(offset, 10);

        if (!Utf8Formatter.TryFormat(value, destination, out _, LengthPropertyFormat))
        {
            throw new InvalidOperationException($"Could not format value {value} into the span at offset {offset}");
        }
    }
    
    private static int GetOffsetPosition(ArrayBufferWriter<byte> sharedBuffer, Action<Utf8JsonWriter> writeAction, Func<ReadOnlySpan<byte>,int> markerAction)
    {
        ArgumentNullException.ThrowIfNull(sharedBuffer);
        ArgumentNullException.ThrowIfNull(writeAction);
        ArgumentNullException.ThrowIfNull(markerAction);
        
        sharedBuffer.Clear();
        using (var writer = new Utf8JsonWriter(sharedBuffer, Utf8WriterOptions))
        {
            writer.WriteStartObject();
            writeAction(writer);
            writer.WriteEndObject();
        }
        var position =  markerAction(sharedBuffer.WrittenSpan);
        return position <= 0 ? throw new InvalidOperationException("Marker not found") : position;
    }
}

public sealed class Int32JsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return int.Parse(reader.GetString()!);
        }
        
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(LogSerializer.JsonInt32Format));
    }
}

public sealed class Int64JsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return long.Parse(reader.GetString()!);
        }
        
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(LogSerializer.JsonInt64Format));
    }
}

public sealed class ByteJsonConverter : JsonConverter<byte>
{
    public override byte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetByte();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return byte.Parse(reader.GetString()!);
        }
        
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, byte value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(LogSerializer.JsonByteFormat));
    }
}