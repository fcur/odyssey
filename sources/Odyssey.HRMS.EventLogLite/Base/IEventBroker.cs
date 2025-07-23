using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventBroker: IEventLogLite
{
}

public interface IEventBroker<TEvent>: IEventBroker where TEvent : class
{
    Task<EventLogResult> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default);

    void Join(IEventConsumer<TEvent> consumer);
    Task<IReadOnlyCollection<LogRespone<TEvent>>> PollEvents(LogSegment logSegment, int batchSize, CancellationToken cancellationToken = default);
}


public sealed record EventLogResult(string TopicName, byte PartitionId, ulong Offset);

public sealed record EventLogTopic(string Value, byte Partitions);



public class LogSegment(byte partitionId)
{
    public byte PartitionId => partitionId;
}