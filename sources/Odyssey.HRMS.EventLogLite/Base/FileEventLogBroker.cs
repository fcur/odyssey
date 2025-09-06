using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;
using System.Collections.Concurrent;
using System.Text;

namespace Odyssey.HRMS.EventLogLite.Base;

// TODO: add compression
// https://github.com/cocowalla/serilog-sinks-file-gzip
public sealed class FileEventLogBroker<TEvent> : IEventBroker<TEvent> where TEvent : class
{
    private readonly ILogger<FileEventLogBroker<TEvent>> _logger;
    private readonly EventBrokerSettings _brokerSettings;
    private readonly IFileEventLogger _eventLogger;
    private readonly IFileEventLogger _offsetLogger;
    private readonly EventLogTopic _topic;
    private readonly EventLogTopic _offsetsTopic;
    // private readonly Channel<LogRespone<TEvent>> _mainChannel;
    private readonly ConcurrentQueue<IEventConsumer<TEvent>> _consumers;

    private byte _partitionsCount = 0;
    private int _tempPartition = 0;
    private ConcurrentDictionary<byte, long> _latestOffsets = null!;
    private Dictionary<byte, FileLogSegment> _segmentsMap = null!;
    private Dictionary<byte, FileLogSegment> _offsetsMap = null!;

    private string _topicRoot;
    private string _offsetsRoot;
    
    
    public FileEventLogBroker(ILogger<FileEventLogBroker<TEvent>> logger, EventBrokerSettings brokerSettings, IFileEventLogger eventLogger, IFileEventLogger offsetLogger, EventLogTopic topic)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(brokerSettings);
        ArgumentNullException.ThrowIfNull(eventLogger);
        ArgumentNullException.ThrowIfNull(offsetLogger);
        ArgumentNullException.ThrowIfNull(topic);

        _logger = logger;
        _brokerSettings = brokerSettings;
        _eventLogger = eventLogger;
        _offsetLogger = offsetLogger;
        _topic = topic;

        _offsetsTopic = new EventLogTopic(_brokerSettings.TopicName, _brokerSettings.Partitions);
        _consumers = [];

        // var opt = new BoundedChannelOptions(1000) { SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        // _mainChannel = Channel.CreateBounded<LogResponse<TEvent>>(opt);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        // TODO: add rebalance
        // NOT possible to decrease partitions count for active topic

        // TODO: add index file for each segment as MMF
        // start consuming from the position of the nearest found offset 
        
        EnsureWorkingDirectory();
        
        var segments = LogSegmentDirectory.Scan(_topic.Name);
        ActivateLatestSegments(segments);
        
        // Scan();
        
        
        InitOffsetTopic();
        await InitBrokerCounters(cancellationToken);
        await AssignConsumers(cancellationToken);
        
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

    public async Task<EventLogResult> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default)
    {
        long newOffset = 0;
        
        var partitionId = GetPartition(request);

        var segment = _segmentsMap[partitionId];
        
        while (true)
        {
            if (!_latestOffsets.TryGetValue(partitionId, out var offsetResult))
            {
                throw new ApplicationException($"No offset found for partition: '{partitionId}'");
            }

            newOffset = offsetResult + 1;
            if (!_latestOffsets.TryUpdate(partitionId, newOffset, offsetResult))
            {
                continue;
            }

            var logMessage = LogMessage<TEvent>.Create(request, newOffset);

            _logger.LogDebug("New message with Key: {Key}, PartitionId: {PartitionId}, Offset: {Offset}", logMessage.Key, segment.Partition, logMessage.Offset);
            
            await _eventLogger.Write(logMessage, segment, cancellationToken);
            break;
        }

        return new EventLogResult(_topic.Name, partitionId, newOffset);
    }

    public void Join(IEventConsumer<TEvent> consumer)
    {
        _consumers.Enqueue(consumer);
    }

    public async Task<IReadOnlyCollection<LogResponse<TEvent>>> PollEvents(PollRequest request, LogSegment logSegment, long offset, CancellationToken cancellationToken = default)
    {
        var result = new List<LogResponse<TEvent>>(request.BatchSize);
        var segment = _segmentsMap[logSegment.Partition];
        
        // TODO: Convert offset to position
        
        await foreach (var logMessage in _eventLogger.Poll<TEvent>(request, segment, offset, cancellationToken))
        {
            var response = new LogResponse<TEvent>
            {
                Key = logMessage.Key,
                Payload = logMessage.Payload,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
                Offset = logMessage.Offset,
                PartitionId = logSegment.Partition,
                Metadata = logMessage.Metadata
            };
            
            result.Add(response);
        }
        
        return result.ToArray();
    }

    public async Task Commit(LogOffsetRequest request, CancellationToken cancellationToken = default)
    {
        var offsetPartitionId = GetPartition(request.Key);
        var offsetFileSegment = _offsetsMap[offsetPartitionId];
        
        var (groupName, topicName, partitionId) = request.Key;
        _ = request.Metadata.TryGetValue("Key", out var itemKey);

        _logger.LogDebug("Offset committing in progress, Key: {Key}, Topic: {TopicName}, Group: {GroupName}, Partition: {PartitionId}, Offset: {Offset}, RequestId: {RequestId}",
            itemKey?.ToString(), topicName, groupName, partitionId, request.Value.Offset, request.RequestId);

        
        var message = new LogOffsetMessage { Key = request.Key, Value = request.Value, Metadata = request.Metadata, OccuredAt = request.OccuredAt };

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
    
    private byte GetPartition(LogRequest<TEvent> request)
    {
        if (request.PartitionId.HasValue)
        {
            return request.PartitionId.Value!;
        }

        if (!string.IsNullOrEmpty(request.Key))
        {
            // partition = murmur2.hash(key) % numPartitions
            var hash = MurmurHash2.Hash32(Encoding.UTF8.GetBytes(request.Key), 42);
            return Convert.ToByte(hash % _partitionsCount);
        }

        Interlocked.Exchange(ref _tempPartition, (_tempPartition + 1) % _partitionsCount);

        return GetRoundRobinPartition();
    }

    private byte GetPartition(LogOffsetKey key)
    {
        var hash = MurmurHash2.Hash32(Encoding.UTF8.GetBytes(key.ToString()), 63);
        return Convert.ToByte(hash % _brokerSettings.Partitions);
    }

    private byte GetRoundRobinPartition()
    {
        Interlocked.Exchange(ref _tempPartition, (_tempPartition + 1) % _partitionsCount);
        return Convert.ToByte(_tempPartition);
    }

    private void EnsureWorkingDirectory()
    {
        _offsetsRoot = LogSegmentDirectory.Init(_offsetsTopic);
        _topicRoot = LogSegmentDirectory.Init(_topic);
    }

    private void InitOffsetTopic()
    {
        // var segmentsMap = FileLogSegment.MapPartitionsWithSegments(_offsetsTopic);

        // _offsetsMap = segmentsMap;
    }
    
    private async Task InitBrokerCounters(CancellationToken cancellationToken)
    {
        // var segmentsMap = FileLogSegment.MapPartitionsWithSegments(_topic);
        // var latestOffsets = await PrepareLatestOffsets(segmentsMap, cancellationToken);
        //
        // _segmentsMap = segmentsMap;
        // _partitionsCount = Convert.ToByte(segmentsMap.Count);
        // _latestOffsets = new ConcurrentDictionary<byte, long>(latestOffsets);
    }

    private Task AssignConsumers(CancellationToken cancellationToken)
    {
        var groupedConsumers = _consumers.GroupBy(v => v.GetConsumerAssigmentState().GroupName).ToArray();
        if (groupedConsumers.Length == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var consumers in groupedConsumers)
        {
            AssignGroupConsumers(consumers.ToArray());
        }

        return Task.CompletedTask;
    }

    private void AssignGroupConsumers(IEventConsumer<TEvent>[] consumers)
    {
        var consumersCount = consumers.Length;

        for (byte partition = 0; partition < _partitionsCount; partition++)
        {
            var consumerIndex = partition % consumersCount;
            var segment = _segmentsMap[partition];

            consumers[consumerIndex].AssignSegment(segment);
        }
    }

    private async Task<Dictionary<byte, long>> PrepareLatestOffsets(Dictionary<byte, FileLogSegment> partitionsMap, CancellationToken cancellationToken)
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

    private void Scan()
    {
        ScanTopic(_offsetsTopic);
        ScanTopic(_topic);
    }
    
    private void ScanTopic(EventLogTopic  topic)
    {
        LogSegmentDirectory.Scan(topic.Name);
    }

    private IReadOnlyCollection<FileLogSegment> ActivateLatestSegments(IReadOnlyCollection<FileLogSegment> segments)
    {
        var groups = segments.OrderByDescending(v => v.BaseOffset).GroupBy(v => v.Partition);
        foreach (var item in groups)
        {
            // item[0] = item[0].Activate();
        }
        


        return segments;
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