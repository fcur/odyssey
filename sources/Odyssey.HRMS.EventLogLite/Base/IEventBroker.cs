using Odyssey.HRMS.EventLogLite.Entities;
using System.Threading.Tasks.Sources;

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
    JoinGroupResponse JoinGroup(JoinGroupRequest request);
    Task<BatchPoolResponse<TEvent>> PollEventsBatch<TEvent>(BatchPoolRequest request, CancellationToken cancellationToken = default) where TEvent : class;
    Task<CommitOffsetResponse> CommitOffset(CommitOffsetRequest request, CancellationToken cancellationToken = default);
    
    // [Obsolete]
    // Task<IReadOnlyCollection<LogResponse<TEvent>>> PollEvents<TEvent>(PollRequest request, LogSegment logSegment, long offset, CancellationToken cancellationToken = default) where TEvent : class;
    Task Commit<TEvent>(LogOffsetRequest request, CancellationToken cancellationToken = default) where TEvent : class;
    // void Join<TEvent>(IEventConsumer<TEvent> consumer) where TEvent : class;
    Task<LogOffsetMessage> ReadSavedOffset(ReadOffsetRequest request, CancellationToken cancellationToken = default);
    // void Join(params ConsumerBrokerConfig[] consumerBrokerConfigs);
    
    Task<HeartBeatResponse> HeartBeat(HeartBeatRequest  request, CancellationToken cancellationToken = default);

    
    SyncGroupResponse SyncGroup(SyncGroupRequest request);
    
    
}


public sealed record HeartBeatRequest(ConsumerMemberId MemberId, ConsumerGroupId GroupId);

public sealed record HeartBeatResponse(string Status, int Code)
{
    public static HeartBeatResponse Alive => new ("alive", 0);
}

public readonly record struct ConsumerMemberId(string Value)
{
    public static implicit operator string (ConsumerMemberId memberId) => memberId.Value;
    public static explicit operator ConsumerMemberId (string memberId) => new (memberId);
    
    
    public override string ToString() => Value;
    
    public bool IsNotSet => string.IsNullOrEmpty(Value);

    public static ConsumerMemberId NotSet => new (string.Empty);
    
    public static ConsumerMemberId CreateNew()
    {
        var id = Guid.CreateVersion7().ToString("D");
        return new ConsumerMemberId(id);
    }
}

public readonly record struct ConsumerGroupId(string Value)
{
    public static implicit operator string (ConsumerGroupId groupId) => groupId.Value;
    public static explicit operator ConsumerGroupId (string groupId) => new (groupId);
    public override string ToString() => Value;
    public static ConsumerGroupId NotSet => new (string.Empty);
    public bool IsNotSet => string.IsNullOrEmpty(Value);
    public bool IsSet => !IsNotSet;
}

public readonly record struct TopicName(string Value)
{
    public static implicit operator string (TopicName topicName) => topicName.Value;
    public static explicit operator TopicName (string topicName) => new (topicName);
    public override string ToString() => Value;
}


public sealed record JoinGroupRequest(int HeartBeatInterval, ConsumerGroupId GroupId, TopicName TopicName, ConsumerMemberId MemberId, int ConsumerGeneration);

public sealed class JoinGroupResponse(ConsumerGroupId GroupId, ConsumerMemberId MemberId, int ConsumerGeneration, long Timestamp, JoinGroupResponseError? Error)
{
    public static JoinGroupResponse IllegalGeneration() => new(ConsumerGroupId.NotSet, ConsumerMemberId.NotSet, 0,0, JoinGroupResponseError.IllegalGeneration());
}

public sealed class JoinGroupResponseError(string ErrorMessage, string ErrorCode)
{
    public static string IllegalGenerationCode = "ILLEGAL_GENERATION";

    public static JoinGroupResponseError IllegalGeneration() => new ("Outdated generation, rejoin required", IllegalGenerationCode);

    public static JoinGroupResponseError? NotSet => null;
}

public sealed class CommitOffsetRequest : IValueTaskSource<bool>
{
    private ManualResetValueTaskSourceCore<bool> _completionEvent = new();

    public ConsumerGroupId GroupId { get; init; }
    public ConsumerMemberId MemberId { get; init; }
    public int ConsumerGeneration { get; init; }
    public CommitOffsetItem[] OffsetItems { get; init; } = null!;
        
    public ValueTask<bool> WaitForCompletion() => new ValueTask<bool>(this, _completionEvent.Version);
    public bool GetResult(short token)=>_completionEvent.GetResult(token);

    public ValueTaskSourceStatus GetStatus(short token) =>_completionEvent.GetStatus(token);

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _completionEvent.OnCompleted(continuation, state, token, flags);
    }
    public void Complete() => _completionEvent.SetResult(true);

    public void Reset() => _completionEvent.Reset();
}

public sealed record CommitOffsetItem(string Topic, byte Partition, long Offset, long Timestamp);


public sealed record CommitOffsetResponse(CommitOffsetError? Error);

public sealed class CommitOffsetError(string ErrorMessage, string ErrorCode);



public sealed record SyncGroupRequest(ConsumerGroupId GroupId, ConsumerMemberId MemberId, int ConsumerGeneration, long Timestamp);
public sealed record SyncGroupResponse(ConsumerGroupId GroupId, ConsumerMemberId MemberId, int ConsumerGeneration, long Timestamp);



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

