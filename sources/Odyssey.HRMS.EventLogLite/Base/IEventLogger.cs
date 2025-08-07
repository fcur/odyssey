using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventLogger<in TSegment> where TSegment : LogSegment
{
    Task WriteBatch<TEvent>(IReadOnlyCollection<LogMessage<TEvent>> logMessages, TSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
    Task Write<TEvent>(LogMessage<TEvent> logMessage, TSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
    Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(TSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
    IAsyncEnumerable<LogMessage<TEvent>> Poll<TEvent>(PollRequest request, TSegment segment, long offset, CancellationToken cancellationToken = default) where TEvent : class;
    Task Commit(LogOffsetRequest request, TSegment segment, CancellationToken cancellationToken = default);
    Task<ReadOffsetResult> ReadSavedOffset(LogOffsetKey key, TSegment segment, CancellationToken cancellationToken = default);
}