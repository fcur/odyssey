using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;
using System.Collections.Concurrent;
using System.Text;

namespace Odyssey.HRMS.EventLogLite.Base;

// TODO: add compression
// https://github.com/cocowalla/serilog-sinks-file-gzip
public sealed class FileEventLogBroker : IEventBroker
//<TEvent> where TEvent : class
{
    private const uint PartitionIdSeed = 42;
    private const uint OffsetIdSeed = 63;
    
    private readonly ILogger<FileEventLogBroker> _logger;
    private readonly EventBrokerSettings _brokerSettings;
    private readonly IFileEventLogger _eventLogger;
    private readonly IFileEventLogger _offsetLogger;

    // private readonly EventLogTopic _eventTopic;
    private readonly EventLogTopic _offsetsTopic;

    // private readonly Channel<LogRespone<TEvent>> _mainChannel;
    // private readonly ConcurrentQueue<IEventConsumer> _consumers;
    // private readonly ConcurrentBag<EventLogTopic> _activeTopics;

    private readonly ConcurrentDictionary<ActiveTopicKey, ActiveTopicInfo> _activeTopicInfos;
    private readonly ConcurrentDictionary<PartitionKey, FileLogSegment> _activeSegments;
    // private readonly ConcurrentDictionary<ActiveTopicKey, ConcurrentQueue<ConsumerGroupReplica>> _consumerGroupReplicasInfo;
    
    
    private readonly ConcurrentDictionary<ActiveTopicKey, ConcurrentDictionary<ConsumerGroupMemberKey, long>> _consumerGroups;
    
    private readonly ConcurrentDictionary<ConsumerGroupIdKey, ConcurrentQueue<PartitionId>> _consumerGroupsAssignment;
    private readonly ConcurrentDictionary<PartitionKey, long> _latestOffsets; // latest active segment 
    private readonly ConcurrentDictionary<string, int> _consumerGenerations;
    
    // private byte _partitionsCount = 0;
    // private int _tempPartition = 0;

    // private Dictionary<byte, FileLogSegment> _segmentsMap = null!;
    private Dictionary<byte, FileLogSegment> _offsetsMap = null!;

    // private string _topicRoot;
    // private string _offsetsRoot;

    // private readonly ConcurrentDictionary<byte, LinkedList<FileLogSegment>> _segmentMap;

    public FileEventLogBroker(ILogger<FileEventLogBroker> logger, EventBrokerSettings brokerSettings, IFileEventLogger eventLogger,
        IFileEventLogger offsetLogger, params EventLogTopic[] topics)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(brokerSettings);
        ArgumentNullException.ThrowIfNull(eventLogger);
        ArgumentNullException.ThrowIfNull(offsetLogger);
        ArgumentNullException.ThrowIfNull(topics);

        _logger = logger;
        _brokerSettings = brokerSettings;
        _eventLogger = eventLogger;
        _offsetLogger = offsetLogger;

        _offsetsTopic = GetOffsetTopic(brokerSettings);
        // _activeTopics = new ConcurrentBag<EventLogTopic>(topics.DistinctBy(v=>v.Name));
        _latestOffsets = [];
        _activeSegments = [];
        // _consumers = [];
        // _segmentMap = [];
        _activeTopicInfos = [];
        _consumerGroups = [];
        _consumerGroupsAssignment = [];
        _consumerGenerations = [];
        // var opt = new BoundedChannelOptions(1000) { SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        // _mainChannel = Channel.CreateBounded<LogResponse<TEvent>>(opt);
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        var loggingRoot = LogSegmentDirectory.GetEventLoggingRoot();
        var knownTopics = LogSegmentDirectory.ScanLoggingRoot(loggingRoot);
        var offsetTopicResult = knownTopics.Single(v => v.Name == _brokerSettings.TopicName);

        var foundOffsetsTopic = new EventLogTopic(offsetTopicResult.Name, (byte)offsetTopicResult.PartitionsWithSegments.Length);
        if (foundOffsetsTopic != _offsetsTopic)
        {
            throw new InvalidDataException("Found invalid offsets topic, broker is not ready to start");
        }

        foreach (var topic in knownTopics)
        {
            if (topic.PartitionsWithSegments.Length == 0)
            {
                continue;
            }

            var topicName = topic.Name;
            var topicKey = new ActiveTopicKey(topicName);

            if (!_consumerGroups.TryGetValue(topicKey, out var consumerGroupReplicasResult))
            {
                _logger.LogWarning("Topic '{TopicName}' does not contain consumers", topicName);
            }
            else
            {
                var consumerGroups = consumerGroupReplicasResult.Keys.GroupBy(v => v.GroupId).ToArray();
                foreach (var groupMembers in consumerGroups)
                {
                    var groupName = groupMembers.Key;
                    if (groupMembers.Count() > 1)
                    {
                        _logger.LogWarning("Topic '{TopicName}' configuration contains a duplicate consumer group '{GroupName}'", topicName, groupName);
                    }
                    
                    var consumersCount = groupMembers.Count();
                    
                    var consumerIndexes = groupMembers.Select(v => 
                        new ConsumerGroupIdKey(v.MemberId, v.GroupId, topicName)).ToArray();

                    var partitionsCount = topic.PartitionsWithSegments.Length;

                    for (byte partition = 0; partition < partitionsCount; partition++)
                    {
                        var consumerIndex = partition % consumersCount;
                        var consumerGroupId = consumerIndexes[consumerIndex];

                        var queue = _consumerGroupsAssignment.GetOrAdd(consumerGroupId, _ => new ConcurrentQueue<PartitionId>());
                        queue.Enqueue(new PartitionId(partition));
                    }
                }
            }

            foreach (var partitionSegment in topic.PartitionsWithSegments)
            {
                // if (partitionSegment.Segments.Length == 0)
                // {
                //     continue;
                // }

                var activeSegment = partitionSegment.Segments.SingleOrDefault(v => v.IsActive) ??
                                    FileLogSegment.New2(partitionSegment.PartitionId, topic.Name);
                
                var partitionKey = new PartitionKey(topic.Name, partitionSegment.PartitionId);
                if (!_activeSegments.TryAdd(partitionKey, activeSegment))
                {
                    throw new InvalidDataException($"Active segment {partitionKey} already exists");
                }

                var logIndex = _eventLogger.ReadLastIndex(activeSegment);
                if (!_latestOffsets.TryAdd(partitionKey, logIndex.Index))
                {
                    throw new InvalidOperationException($"Can't save offset for partition: '{partitionKey}'");
                }

            }
        }
        
        return Task.CompletedTask;


        // ensure working directory
        // _offsetsRoot = LogSegmentDirectory.GetOrCreate(_offsetsTopic);
        // _topicRoot = LogSegmentDirectory.GetOrCreate(_eventTopic);


        var offsetTopicSegments = LogSegmentDirectory.ScanOffsets(_offsetsTopic.Name);

        // TODO: add rebalance
        // NOT possible to decrease partitions count for active topic

        // TODO: add index file for each segment as MMF
        // start consuming from the position of the nearest found offset 


        // var topicSegments = LogSegmentDirectory.ScanOffsets(_eventTopic.Name);
        // if (!topicSegments.Any())
        // {
        //     topicSegments = LogSegmentDirectory.Init(_topic.Name);
        // }

        // foreach (var item in topicSegments)
        // {
        //     _segmentMap.AddOrUpdate(item.Key, item.Value, (key, oldValue) => item.Value);
        // }

        // Consuming: TBD
        // InitOffsetTopic();
        // await InitBrokerCounters(cancellationToken);
        // await AssignConsumers(cancellationToken);

        //_ = Task.Factory.StartNew(async () => await StartConsumePublishedEventsInternal(cancellationToken), TaskCreationOptions.LongRunning).Unwrap();
    }

    // private async Task StartConsumePublishedEventsInternal(CancellationToken cancellationToken)
    // {
    //     while (await _mainChannel.Reader.WaitToReadAsync(cancellationToken))
    //     {
    //         if (_mainChannel.Reader.TryRead(out var item))
    //         {
    //             // TBD: publish to all consumers
    //         }
    //     }
    // }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<EventLogResult> LogEvent<TEvent>(LogRequest<TEvent> request, CancellationToken cancellationToken = default) where TEvent : class
    {
        var partitionId = GetPartition(request);
        var partitionKey = new PartitionKey(request.TopicName, partitionId);
        long offsetResult;
        var segment = _activeSegments[partitionKey];

        while (true)
        {
            if (!_latestOffsets.TryGetValue(partitionKey, out offsetResult))
            {
                throw new ApplicationException($"No offset found for partition: '{partitionId}'");
            }

            if (!_latestOffsets.TryUpdate(partitionKey, offsetResult + 1, offsetResult))
            {
                continue;
            }

            var logMessage = LogMessage<TEvent>.Create(request, offsetResult);

            _logger.LogDebug("New message with Key: {Key}, PartitionId: {PartitionId}, Offset: {Offset}", logMessage.Key, segment.Partition,
                logMessage.Offset);

            var positionPair = await _eventLogger.Write(logMessage, segment, cancellationToken);
            break;
        }

        return new EventLogResult(request.TopicName, partitionId, offsetResult);
    }

    // public void Join<TEvent>(IEventConsumer<TEvent> consumer) where TEvent : class
    // {
    //     _consumers.Enqueue(consumer);
    //     // _consumerTopics.Enqueue(consumer);
    // }

    // public void Join(EventLogTopic topic)
    // {
    //     _activeTopics.Add(topic);
    // }

    public async Task<BatchPoolResponse<TEvent>> PollEventsBatch<TEvent>(BatchPoolRequest request, CancellationToken cancellationToken = default) where TEvent : class
    {
        // var result = new List<LogResponse<TEvent>>(batchPoolRequest.MaxBytes);
        
        var partitionKey = new PartitionKey(request.TopicName, request.PartitionId);
        if (!_activeSegments.TryGetValue(partitionKey, out var activeSegment))
        {
            return new BatchPoolResponse<TEvent> { Error = BatchPoolResponseError.UnknownPartition(partitionKey)};
        }

        var startPositionResult = _eventLogger.FindNearestPosition(request.Offset, activeSegment);
        
        var pollRequest = new PollRequest
        {
            // TopicName =  request.TopicName,
            // GroupName =   request.GroupName,
            // RequestId = request.RequestId,
            // OccuredAt = request.OccuredAt,
            StartPosition = startPositionResult.Position
        };

        var sizeLimit = request.MaxBytes;
        
        var batchItems = new  List<LogResponse<TEvent>>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // cts.CancelAfter(request.MaxWaitTimeMs);

        try
        {
            await foreach (var logMessage in _eventLogger.Poll<TEvent>(pollRequest, activeSegment, cts.Token).ConfigureAwait(false))
            {
                sizeLimit -= logMessage.PayloadLength;
            
                if (sizeLimit <= 0)
                {
                    await cts.CancelAsync().ConfigureAwait(false);
                    break;
                }
                if (logMessage.Offset < request.Offset)
                {
                    continue;
                }
            
                var logResponse = new LogResponse<TEvent>
                {
                    Key = logMessage.Key,
                    Payload = logMessage.Payload,
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
                    Offset = logMessage.Offset,
                    PartitionId = activeSegment.Partition,
                    Metadata = logMessage.Metadata
                };

                batchItems.Add(logResponse);
            }
        }
        catch (OperationCanceledException)
        {
            var cancellationReason = cancellationToken.IsCancellationRequested? "Cancelled from client's code":
                cts.Token.IsCancellationRequested? "Cancelled by broker": $"Timeout {request.MaxWaitTimeMs}ms has expired";
                
            _logger.LogDebug("Polling cancelled, Reason: {Reason}",cancellationReason);
        }
        
        var result = new BatchPoolResponse<TEvent> { Items = batchItems, TopicName = request.TopicName, ResponseId = request.RequestId };
        
        return result;
    }

    public Task<CommitOffsetResponse> CommitOffset(CommitOffsetRequest request, CancellationToken cancellationToken = default)
    {
        var batchItems = request.OffsetItems.Select(v => new LogMessageBatchItem<LogCommitKey, LogCommitValue> { })
            .ToArray();
        
        var batch = new LogMessageBatch<LogCommitKey, LogCommitValue>
        {
            Payload = batchItems
        };
        
        throw new NotImplementedException();
    }

    // [Obsolete]
    // public async Task<IReadOnlyCollection<LogResponse<TEvent>>> PollEvents<TEvent>(PollRequest request, LogSegment logSegment, long offset,
    //     CancellationToken cancellationToken = default) where TEvent : class
    // {
    //     // var result = new List<LogResponse<TEvent>>(request.BatchSize);
    //     var result = new List<LogResponse<TEvent>>(1000);
    //     var segmentKey = new PartitionKey(request.TopicName, logSegment.Partition);
    //     var segment = _activeSegments[segmentKey];
    //     // var segment = _segmentsMap[logSegment.Partition];
    //
    //     // TODO: Convert offset to position
    //
    //     await foreach (var logMessage in _eventLogger.Poll<TEvent>(request, segment, cancellationToken))
    //     {
    //         var response = new LogResponse<TEvent>
    //         {
    //             Key = logMessage.Key,
    //             Payload = logMessage.Payload,
    //             Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
    //             Offset = logMessage.Offset,
    //             PartitionId = logSegment.Partition,
    //             Metadata = logMessage.Metadata
    //         };
    //
    //         result.Add(response);
    //     }
    //
    //     return result.ToArray();
    // }

    public async Task Commit<TEvent>(LogOffsetRequest request, CancellationToken cancellationToken = default) where TEvent : class
    {
        var offsetPartitionId = GetPartition(request.Key);
        var offsetFileSegment = _offsetsMap[offsetPartitionId];

        var (groupName, topicName, partitionId) = request.Key;
        _ = request.Metadata.TryGetValue("Key", out var itemKey);

        _logger.LogDebug(
            "Offset committing in progress, Key: {Key}, Topic: {TopicName}, Group: {GroupName}, Partition: {PartitionId}, Offset: {Offset}, RequestId: {RequestId}",
            itemKey?.ToString(), topicName, groupName, partitionId, request.Value.Offset, request.RequestId);

        var occuredAt = DateTimeOffset.FromUnixTimeMilliseconds(request.Value.CommitTimestamp);
        var message = new LogOffsetMessage { Key = request.Key, Value = request.Value, Metadata = request.Metadata, OccuredAt = occuredAt };

        // TBD
        var newOffset = 0;

        var logMessage = LogMessage<LogOffsetMessage>.Create(request.Key.ToString(), message, newOffset);

        var position = await _offsetLogger.Write(logMessage, offsetFileSegment, cancellationToken);
        // var position = await _eventLogger.Commit(request, offsetFileSegment, cancellationToken);
    }

    public async Task<LogOffsetMessage> ReadSavedOffset(ReadOffsetRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
        // var offsetPartitionId = GetPartition(request.Key);
        // var offsetFileSegment = _offsetsMap[offsetPartitionId];
        //
        // var (groupName, topicName, partitionId) = request.Key;
        //
        // _logger.LogDebug("Offset reading in progress, Topic: {TopicName}, Group: {GroupName}, Partition: {PartitionId}, RequestId: {RequestId}",
        //     topicName, groupName, partitionId, request.RequestId);
        //
        // // TBD: position in file
        // var offset = 0;
        // var pollRequest = new PollRequest
        // {
        //     BatchSize = 10000,
        //     TopicName =  _offsetsTopic.Name,
        //     // not required
        //     GroupName = nameof(PollRequest.GroupName),
        //     RequestId = request.RequestId,
        //     OccuredAt = request.OccuredAt
        // };
        //
        // while (true)
        // {
        //     await foreach (var logMessage in _offsetLogger.Poll<LogOffsetMessage>(pollRequest, offsetFileSegment, offset, cancellationToken))
        //     {
        //         if (logMessage.Key != request.Key)
        //         {
        //             continue;
        //         }
        //     
        //     }
        // }
        //
        //
        //
        //
        // var offsetLogMessage = await _offsetLogger.ReadLast<LogOffsetMessage>(offsetFileSegment, cancellationToken);
        // if (offsetLogMessage == null)
        // {
        //     return LogOffsetMessage.CreateNew(request.Key);
        // }
        //
        // // var result =  _eventLogger.ReadSavedOffset(request.Key, offsetFileSegment, cancellationToken);
        // return offsetLogMessage.Payload;
    }

    // [Obsolete]
    // private byte GetPartitionObsolete<TEvent>(LogRequest<TEvent> request) where TEvent : class
    // {
    //     if (request.PartitionId.HasValue)
    //     {
    //         return request.PartitionId.Value!;
    //     }
    //
    //     if (!string.IsNullOrEmpty(request.Key))
    //     {
    //         // partition = murmur2.hash(key) % numPartitions
    //         var hash = MurmurHash2.Hash32(Encoding.UTF8.GetBytes(request.Key), 42);
    //         return Convert.ToByte(hash % _partitionsCount);
    //     }
    //
    //     Interlocked.Exchange(ref _tempPartition, (_tempPartition + 1) % _partitionsCount);
    //
    //     return GetRoundRobinPartition();
    // }

    private byte GetPartition<TEvent>(LogRequest<TEvent> request) where TEvent : class
    {
        if (request.PartitionId.HasValue)
        {
            return request.PartitionId.Value;
        }

        byte result;
        var key = new ActiveTopicKey(request.TopicName);

        while (true)
        {
            if (!_activeTopicInfos.TryGetValue(key, out var topicInfo))
            {
                throw new ApplicationException($"No active topic '{request.TopicName}' found");
            }

            if (!string.IsNullOrEmpty(request.Key))
            {
                // TODO: test MurmurHash3 X 
                // partition = murmur2.hash(key) % numPartitions
                var hash = MurmurHash2.Hash32(Encoding.UTF8.GetBytes(request.Key), PartitionIdSeed);
                result = Convert.ToByte(hash % topicInfo.PartitionsCount);
                break;
            }

            var nextInfo = topicInfo.NextRoundRobinInfo();
            if (!_activeTopicInfos.TryUpdate(key, nextInfo, topicInfo))
            {
                // another thread was ahead
                continue;
            }

            result = nextInfo.TempPartition;
            break;
        }

        return result;
    }

    private byte GetPartition(LogOffsetKey key)
    {
        var hash = MurmurHash2.Hash32(Encoding.UTF8.GetBytes(key.ToString()), OffsetIdSeed);
        return Convert.ToByte(hash % _brokerSettings.Partitions);
    }

    // private byte GetRoundRobinPartition()
    // {
    //     Interlocked.Exchange(ref _tempPartition, (_tempPartition + 1) % _partitionsCount);
    //     return Convert.ToByte(_tempPartition);
    // }

    // private void InitOffsetTopic()
    // {
    //     var segmentsMap = FileLogSegment.MapPartitionsWithSegments(_offsetsTopic);
    //
    //     _offsetsMap = segmentsMap;
    // }

    // private async Task InitBrokerCounters(CancellationToken cancellationToken)
    // {
    //     var segmentsMap = FileLogSegment.MapPartitionsWithSegments(_topic);
    //     var latestOffsets = await PrepareLatestOffsets(segmentsMap, cancellationToken);
    //     
    //     _segmentsMap = segmentsMap;
    //     _partitionsCount = Convert.ToByte(segmentsMap.Count);
    //     _latestOffsets = new ConcurrentDictionary<byte, long>(latestOffsets);
    // }

    // private Task AssignConsumers<TEvent>(CancellationToken cancellationToken) where TEvent : class
    // {
    //     var groupedConsumers = _consumers.GroupBy(v => v.GetConsumerAssigmentState().GroupName).ToArray();
    //     if (groupedConsumers.Length == 0)
    //     {
    //         return Task.CompletedTask;
    //     }
    //
    //     foreach (var consumers in groupedConsumers)
    //     {
    //         AssignGroupConsumers(consumers.ToArray());
    //     }
    //
    //     return Task.CompletedTask;
    // }

    // private void AssignGroupConsumers<TEvent>(IEventConsumer<TEvent>[] consumers) where TEvent : class
    // {
    //     var consumersCount = consumers.Length;
    //
    //     for (byte partition = 0; partition < _partitionsCount; partition++)
    //     {
    //         var consumerIndex = partition % consumersCount;
    //         var segment = _segmentsMap[partition];
    //
    //         consumers[consumerIndex].AssignSegment(segment);
    //     }
    // }

    private async Task<Dictionary<byte, long>> PrepareLatestOffsets<TEvent>(Dictionary<byte, FileLogSegment> partitionsMap,
        CancellationToken cancellationToken) where TEvent : class
    {
        var result = new Dictionary<byte, long>();

        foreach (var item in partitionsMap)
        {
            var segment = item.Value;
            var latestMsg = await _eventLogger.ReadLast<TEvent>(segment, cancellationToken);
            var offset = latestMsg?.Offset ?? 0L;

            result.Add(item.Key, offset);
        }

        return result;
    }

    // private void Scan()
    // {
    //     ScanTopic(_offsetsTopic);
    //     ScanTopic(_eventTopic);
    // }

    // private void ScanTopic(EventLogTopic topic)
    // {
    //     LogSegmentDirectory.ScanOffsets(topic.Name);
    // }

    private static EventLogTopic GetOffsetTopic(EventBrokerSettings brokerSettings)
    {
        return new EventLogTopic(brokerSettings.TopicName, brokerSettings.Partitions);
    }

    public void Join(params ProducerBrokerConfig[] producerBrokerConfigs)
    {
        if (producerBrokerConfigs.Length == 0)
        {
            return;
        }

        foreach (var config in producerBrokerConfigs)
        {
            var key = new ActiveTopicKey(config.TopicName);
            var val = new ActiveTopicInfo(config.Partitions, 0);
            _activeTopicInfos.AddOrUpdate(key, val, (k, v) => val);
        }
    }

    // public void Join(params ConsumerBrokerConfig[] consumerBrokerConfigs)
    // {
    //     if (consumerBrokerConfigs.Length == 0)
    //     {
    //         return;
    //     }
    //
    //     foreach (var config in consumerBrokerConfigs)
    //     {
    //         var key = new ActiveTopicKey(config.TopicName);
    //         var val = new ConsumerGroupReplica(config.GroupName, config.Replicas);
    //
    //         var queue = _consumerGroupReplicasInfo.GetOrAdd(key, _ => new ConcurrentQueue<ConsumerGroupReplica>());
    //         queue.Enqueue(val);
    //     }
    // }

    public Task<HeartBeatResponse> HeartBeat(HeartBeatRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public JoinGroupResponse JoinGroup(JoinGroupRequest request)
    {
        var topicKey = new ActiveTopicKey(request.TopicName);
        var memberId = request.MemberId.IsNotSet? ConsumerMemberId.CreateNew() : request.MemberId;

        if (request.GroupId.IsSet && _consumerGenerations.TryGetValue(request.GroupId, out var generation) && generation > request.ConsumerGeneration)
        {
            return JoinGroupResponse.IllegalGeneration();
        }
        
        while (true)
        {
            var topicGroups = _consumerGroups.GetOrAdd(topicKey, _ => new ConcurrentDictionary<ConsumerGroupMemberKey, long>());
            var memberKey = new ConsumerGroupMemberKey(request.GroupId, memberId);
            var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // add new member and rebalance
            if (!topicGroups.TryGetValue(memberKey, out var joiningTime) && topicGroups.TryAdd(memberKey, time))
            {        
                // TODO: rebalance and update consumerGenerationId
                var generation1 = _consumerGenerations.AddOrUpdate(request.GroupId, 1, (key, old) => old + 1);
        
                return new JoinGroupResponse(request.GroupId, memberId, generation1, time, JoinGroupResponseError.NotSet);
            }

            // just update time
            if (topicGroups.TryUpdate(memberKey, time, joiningTime))
            {
                return new JoinGroupResponse(request.GroupId, memberId, _consumerGenerations[request.GroupId], time, JoinGroupResponseError.NotSet);
            }
        }
    }

    public SyncGroupResponse SyncGroup(SyncGroupRequest request)
    {
        throw new NotImplementedException();
    }
}

public interface IFileLogCleaner : IEventLogLite
{
}

// TODO: check log segments in background
// STAGE1: compact offsets
// STAGE2: archive|rename *.del
// STAGE3: delete
public sealed class FileLogCleaner : IFileLogCleaner
{
    public Task Start(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

public sealed record ConsumerInfo(byte Id, string TopicName, string GroupName);

public sealed record ProducerInfo(byte Id, string TopicName);

public readonly record struct PartitionKey(string TopicName, byte PartitionId);

public readonly record struct ActiveTopicKey(string TopicName);

public readonly record struct ActiveTopicInfo(byte PartitionsCount, byte TempPartition)
{
    public ActiveTopicInfo NextRoundRobinInfo()
    {
        return this with { TempPartition = (byte)(TempPartition + 1 % PartitionsCount) };
    }
}

public readonly record struct ConsumerGroupReplica(string GroupName, byte Replicas);

public readonly record struct ConsumerGroupMemberKey(ConsumerGroupId GroupId, ConsumerMemberId MemberId);

public readonly record struct ConsumerGroupIdKey(string MemberId, string GroupName, string TopicName);

public readonly record struct PartitionId(byte Value);

