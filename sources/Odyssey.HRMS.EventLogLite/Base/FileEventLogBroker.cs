using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Text;

namespace Odyssey.HRMS.EventLogLite.Base;

// TODO: add compression
// https://github.com/cocowalla/serilog-sinks-file-gzip
public sealed class FileEventLogBroker<TEvent> : IEventBroker<TEvent> where TEvent : class
{
    private readonly IFileEventLogger _eventLogger;
    private readonly EventLogTopic _topic;
    // private readonly Channel<LogRespone<TEvent>> _mainChannel;
    private readonly ConcurrentQueue<IEventConsumer<TEvent>> _consumers;

    private byte _partitionsCount = 0;
    private int _tempPartition = 0;
    private ConcurrentDictionary<byte, ulong> _offsets = null!;
    private Dictionary<byte, FileLogSegment> _segmentsMap = null!;

    public FileEventLogBroker(IFileEventLogger eventLogger, EventLogTopic topic)
    {
        ArgumentNullException.ThrowIfNull(eventLogger);
        ArgumentNullException.ThrowIfNull(topic);

        _eventLogger = eventLogger;
        _topic = topic;
        _consumers = [];

        // var opt = new BoundedChannelOptions(1000) { SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        // _mainChannel = Channel.CreateBounded<LogResponse<TEvent>>(opt);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        // TODO: add rebalance
        // NOT possible to decrease partitions count for active topic

        await InitWorkingDirectory(cancellationToken);
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
        ulong newOffset = 0;
        
        var partitionId = GetPartition(request);

        var segment = _segmentsMap[partitionId];
        
        while (true)
        {
            if (!_offsets.TryGetValue(partitionId, out var offsetResult))
            {
                throw new ApplicationException($"No offset found for partition: '{partitionId}'");
            }

            newOffset = offsetResult + 1;
            if (!_offsets.TryUpdate(partitionId, newOffset, offsetResult))
            {
                continue;
            }

            var logMessage = LogMessage<TEvent>.Create(request, newOffset);
            await _eventLogger.Write(logMessage, segment, cancellationToken);

            // var response = new LogRespone<TEvent>
            // {
            //     Key = logMessage.Key,
            //     Payload = logMessage.Payload,
            //     Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
            //     Offset = logMessage.Offset,
            //     PartitionId = partitionId
            // };
            //
            // await _mainChannel.Writer.WriteAsync(response, cancellationToken);
            break;
        }

        return new EventLogResult(_topic.Value, partitionId, newOffset);
    }

    public void Join(IEventConsumer<TEvent> consumer, CancellationToken cancellationToken = default)
    {
        _consumers.Enqueue(consumer);
    }

    private async Task<IReadOnlyCollection<LogRespone<TEvent>>> PullEvents(FileLogSegment logSegment, CancellationToken cancellationToken = default)
    {
        const int batchSize = 100;
        var pullDuration = TimeSpan.FromSeconds(10);

        var result = new List<LogRespone<TEvent>>(batchSize);
        var cts = new CancellationTokenSource(pullDuration);
        var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
        
        await foreach (var logMessage in _eventLogger.Pull<TEvent>(logSegment, batchSize, tokenSource.Token))
        {
            var response = new LogRespone<TEvent>
            {
                Key = logMessage.Key,
                Payload = logMessage.Payload,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
                Offset = logMessage.Offset,
                PartitionId = logSegment.PartitionId
            };
            
            result.Add(response);
        }
        
        return result.ToArray();
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

    private byte GetRoundRobinPartition()
    {
        Interlocked.Exchange(ref _tempPartition, (_tempPartition + 1) % _partitionsCount);
        return Convert.ToByte(_tempPartition);
    }

    private Task InitWorkingDirectory(CancellationToken cancellationToken)
    {
        FileLogSegment.InitWorkingDirectory(_topic);

        return Task.CompletedTask;
    }

    private async Task InitBrokerCounters(CancellationToken cancellationToken)
    {
        var segmentsMap = FileLogSegment.MapPartitionsWithSegments(_topic);
        var initialOffsets = await PrepareInitialOffsets(segmentsMap, cancellationToken);

        _segmentsMap = segmentsMap;
        _partitionsCount = Convert.ToByte(segmentsMap.Count);
        _offsets = new ConcurrentDictionary<byte, ulong>(initialOffsets);
    }

    private Task AssignConsumers(CancellationToken cancellationToken)
    {
        var groupedConsumers = _consumers.GroupBy(v => v.GetGroupName()).ToArray();
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

    private async Task<Dictionary<byte, ulong>> PrepareInitialOffsets(Dictionary<byte, FileLogSegment> partitionsMap, CancellationToken cancellationToken)
    {
        var result = new Dictionary<byte, ulong>();

        foreach (var item in partitionsMap)
        {
            var segment = item.Value;
            var latestMsg = await _eventLogger.ReadLastMessage<TEvent>(segment, cancellationToken);
            var offset = latestMsg?.Offset ?? 0UL;

            result.Add(item.Key, offset);
        }

        return result;
    }
}

public interface IFileLogCleaner : IEventLogLite
{
}

// TODO: check log segments in background
// STAGE1: archive|rename *.del
// STAGE2: delete
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