using Odyssey.HRMS.EventLogLite.Entities;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Odyssey.HRMS.EventLogLite;

public static class LogSerializer
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly StandardFormat LengthPropertyFormat = new ('D', 10);
    private static readonly int RecordLengthLogMessageOffset;
    private const string EmptyRecordLength = "0000000000";
    private const byte JsonLineDivider = 10;

    static LogSerializer()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(LogMessage.RecordLength), EmptyRecordLength);
            writer.WriteEndObject();
        }

        RecordLengthLogMessageOffset = buffer.WrittenSpan.IndexOf("0000000000"u8);
    }

    public static int SerializeAsJsonRow<TEvent>(ArrayBufferWriter<byte> sharedBuffer, LogMessage<TEvent> message) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(sharedBuffer);
        ArgumentNullException.ThrowIfNull(message);
        
        var startPosition = sharedBuffer.WrittenCount;
        var keyBytesLength = System.Text.Encoding.UTF8.GetByteCount(message.Key);
        int currentPayloadLengthOffset, payloadLength,  currentMetadataLengthOffset = 0, metadataLength = 0;
        
        using (var writer = new Utf8JsonWriter(sharedBuffer))
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
        var writableSpan = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(messageSpan), recordLength);

        UpdatePropertyLength(writableSpan, RecordLengthLogMessageOffset, recordLength);
        UpdatePropertyLength(writableSpan, currentPayloadLengthOffset, payloadLength);
        if (currentMetadataLengthOffset > 0)
        {
            UpdatePropertyLength(writableSpan, currentMetadataLengthOffset, metadataLength);
        }

        return recordLength;
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
}