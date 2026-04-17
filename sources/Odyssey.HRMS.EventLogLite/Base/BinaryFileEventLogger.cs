using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

public sealed class BinaryFileEventLogger: IFileEventLogger
{
    // [Record Length]
    // ├─ [Attributes]
    // ├─ [Timestamp]
    // ├─ [Offset]
    // ├─ [Key Length]
    // ├─ [Key]
    // ├─ [Value Length]
    // ├─ [Value]

    public Task<PositionPair>  WriteBatch<TEvent>(IReadOnlyCollection<LogMessage<TEvent>> logMessages, FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class
    {
        // offset of the first record in the batch
        long batchOffset = 0;
        // offset ot the last record in the batch
        long lastOffset = 0;
        // number of records in batch
        int count = 0;  
        // record sequence number
        int sequence = 0;
        byte attributes = 0;
        // Crc32C
        int checksum = 0;
        uint keyLength = 0;
        ulong valueLength = 0;
        
        throw new NotImplementedException();
    }

    public Task<PositionPair> Write<TEvent>(LogMessage<TEvent> logMessage, FileLogSegment segment, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        int recordLength = 0;
        byte attributes = 0;
        long timestamp;
        long offset;
        uint keyLength = 0;
        ulong valueLength = 0;
        
        throw new NotImplementedException();
    }

    public Task<LogMessage<TEvent>?> ReadLast<TEvent>(FileLogSegment segment, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<LogMessage<TEvent>> Poll<TEvent>(PollRequest request, FileLogSegment segment, CancellationToken cancellationToken = default)  where TEvent : class
    {
        throw new NotImplementedException();
    }

    public LogIndex ReadLastIndex(FileLogSegment segment)
    {
        throw new NotImplementedException();
    }

    public Task<PositionPair> Commit(LogOffsetRequest request, FileLogSegment segment, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<LogOffsetMessage> ReadSavedOffset(LogOffsetKey key, FileLogSegment segment, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}