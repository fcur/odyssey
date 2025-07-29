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
    Task<ReadOffsetResult> ReadLatestOffset(ReadOffsetRequest request, CancellationToken cancellationToken = default);
}


public sealed record EventLogResult(string TopicName, byte PartitionId, long Offset);

public sealed record EventLogTopic(string Value, byte Partitions);



public class LogSegment(byte partitionId)
{
    public byte PartitionId => partitionId;
}

public sealed class LogSegmentWithOffset(byte partitionId, ulong offset) : LogSegment(partitionId)
{
    public ulong Offset => offset;
}