using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

// public interface IEventBroker: IEventLogLite
// {
// }

public interface IEventBroker: IEventLogLite, IEventProducerBroker, IEventConsumerBroker
{
}


public sealed record ProducerBrokerConfig(string TopicName, byte Partitions);

public sealed record ConsumerBrokerConfig(string TopicName, string GroupName, byte Replicas);



public interface IEventProducerBroker
{
    Task<EventLogResult> LogEvent<TEvent>(LogRequest<TEvent> request, CancellationToken cancellationToken = default) where TEvent : class;
    void Join(params ProducerBrokerConfig[] producerBrokerConfigs);
}


public interface IEventConsumerBroker
{
    Task<IReadOnlyCollection<LogResponse<TEvent>>> PollEventsBatch<TEvent>(BatchPoolRequest batchPoolRequest, CancellationToken cancellationToken = default) where TEvent : class;
    [Obsolete]
    Task<IReadOnlyCollection<LogResponse<TEvent>>> PollEvents<TEvent>(PollRequest request, LogSegment logSegment, long offset, CancellationToken cancellationToken = default) where TEvent : class;
    Task Commit<TEvent>(LogOffsetRequest request, CancellationToken cancellationToken = default) where TEvent : class;
    // void Join<TEvent>(IEventConsumer<TEvent> consumer) where TEvent : class;
    Task<LogOffsetMessage> ReadSavedOffset(ReadOffsetRequest request, CancellationToken cancellationToken = default);
    // void Join(params ConsumerBrokerConfig[] consumerBrokerConfigs);
    
    Task<HeartBeatResponse> HeartBeat(HeartBeatRequest  request, CancellationToken cancellationToken = default);

    JoinGroupResponse JoinGroup(JoinGroupRequest request);
    
    SyncGroupResponse SyncGroup(SyncGroupRequest request);
}


public sealed record HeartBeatRequest(ConsumerMemberId MemberId, ConsumerGroupName GroupName);

public sealed record HeartBeatResponse(string Status, int Code)
{
    public static HeartBeatResponse Alive => new ("alive", 0);
}

public readonly record struct ConsumerMemberId(string Value);

public readonly record struct ConsumerGroupName(string Value);

public sealed record JoinGroupRequest(int HeartBeatInterval, string GroupName, string TopicName);
public sealed record JoinGroupResponse();

public sealed record SyncGroupRequest();
public sealed record SyncGroupResponse();



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
public sealed record EventLogTopicScanResult(string Name, PartitionSegments[] PartitionsWithSegments)
{
    private byte Partitions => Convert.ToByte(PartitionsWithSegments.Length);
}

public sealed record PartitionSegments(byte PartitionId,  string Path, FileLogSegment[]  Segments);

