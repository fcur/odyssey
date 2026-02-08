using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventBroker: IEventLogLite
{
}

public interface IEventBroker<TEvent>: IEventBroker where TEvent : class
{
    Task<EventLogResult> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default);
    void Join(IEventConsumer<TEvent> consumer);
    Task<IReadOnlyCollection<LogResponse<TEvent>>> PollEvents(PollRequest request, LogSegment logSegment, long offset, CancellationToken cancellationToken = default);
    Task Commit(LogOffsetRequest request, CancellationToken cancellationToken = default);
    Task<LogOffsetMessage> ReadSavedOffset(ReadOffsetRequest request, CancellationToken cancellationToken = default);
}


public sealed record EventLogResult(string TopicName, byte PartitionId, long Offset);

public sealed record EventLogTopic(string Name, byte Partitions);

/* topic scan result structure:
 * - name
 * - partition-segments
 *  - partition-id
 *  - path
 *  - segments
 *   - partition
 *   - topic-root
 *   - base-offset
 *   - base-time
 *   - size
 *   - is-active
 */
public sealed record EventLogTopicScanResult(string Name, PartitionSegments[] PartitionSegments)
{
    private byte Partitions => Convert.ToByte(PartitionSegments.Length);
}

public sealed record PartitionSegments(byte PartitionId,  string Path, FileLogSegment[]  Segments);

