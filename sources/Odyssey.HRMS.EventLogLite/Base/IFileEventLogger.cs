using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IFileEventLogger : IEventLogger<FileLogSegment>
{
    // new Task WriteBatch<TEvent>(IReadOnlyCollection<LogMessage<TEvent>> logMessages, FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
    // new Task Write<TEvent>(LogMessage<TEvent> logMessage, FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
    // new Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
    // new IAsyncEnumerable<LogMessage<TEvent>> Poll<TEvent>(PollRequest request, FileLogSegment segment, long offset, CancellationToken cancellationToken = default) where TEvent : class;
    // new Task Commit(LogOffsetRequest request, FileLogSegment segment, CancellationToken cancellationToken = default);
    // new Task<ReadOffsetResult> ReadSavedOffset(LogOffsetKey key, FileLogSegment segment, CancellationToken cancellationToken = default);
}