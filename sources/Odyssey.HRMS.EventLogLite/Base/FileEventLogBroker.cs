using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Base;

// TODO: add compression
// https://github.com/cocowalla/serilog-sinks-file-gzip
public sealed class FileEventLogBroker<TEvent> : IEventBroker<TEvent> where TEvent : class
{
    private readonly EventLogTopic _topic;
    private readonly Channel<LogRespone<TEvent>> _mainChannel;
    private readonly ConcurrentQueue<IEventConsumer<TEvent>> _consumers;
    
    private string _workingDirectory = null!;
    private byte _partitionsCount = 0;
    private int _tempPartition = 0;
    private ConcurrentDictionary<byte, ulong> _offsets = null!;
    private Dictionary<byte, string> _partitionsMap = null!;

    // log divider symbol, equals to '\n'
    private const byte EventLogDivider = 10;
    private const string EventLogFileExtension = ".log";

    public FileEventLogBroker(EventLogTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        _topic = topic;
        _consumers = [];

        var opt = new BoundedChannelOptions(1000) { SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        _mainChannel = Channel.CreateBounded<LogRespone<TEvent>>(opt);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        // TODO: add rebalance
        // NOT possible to decrease partitions count

        InitWorkingDirectory();
        await InitCounters(cancellationToken);
        await AssignConsumers(cancellationToken);
        
        //_ = Task.Factory.StartNew(async () => await StartConsumePublishedEventsInternal(cancellationToken), TaskCreationOptions.LongRunning).Unwrap();
    }

    private async Task StartConsumePublishedEventsInternal(CancellationToken cancellationToken)
    {
        while (await _mainChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            if (_mainChannel.Reader.TryRead(out var item))
            {
                // TBD: publish to all consumers
            }
        }
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<EventLogResult> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default)
    {
        var partitionId = GetPartition(request);
        var logFilePath = _partitionsMap[partitionId];

        _offsets.TryGetValue(partitionId, out var offsetResult);

        var logMessage = LogMessage<TEvent>.Create(request, offsetResult);

        await using (var fs = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.Write))
        {
            fs.Seek(0, SeekOrigin.End);

            await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);
            await JsonSerializer.SerializeAsync(fs, logMessage, new JsonSerializerOptions { WriteIndented = false }, cancellationToken);
        }

        var response = new LogRespone<TEvent>
        {
            Key = logMessage.Key,
            Payload = logMessage.Payload,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
            Offset = logMessage.Offset,
            PartitionId = partitionId
        };

        await _mainChannel.Writer.WriteAsync(response, cancellationToken);

        return new EventLogResult(_topic.Value, partitionId, logMessage.Offset);
    }

    public void Join(IEventConsumer<TEvent> consumer, CancellationToken cancellationToken = default)
    {
        _consumers.Enqueue(consumer);
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

    private async Task<LogMessage<TEvent>?> ReadLatestMessage(string filePath, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read);
        if (fs.Length == 0)
        {
            return null;
        }

        fs.Seek(-1, SeekOrigin.End);
        var readBuffer = new byte[1];
        var writeBuffer = new Stack<byte>();

        while (fs.Position > 0)
        {
            await fs.ReadExactlyAsync(readBuffer, 0, 1, cancellationToken);
            if (readBuffer[0] == EventLogDivider)
            {
                break;
            }

            writeBuffer.Push(readBuffer[0]);
            fs.Seek(-2, SeekOrigin.Current);
        }


        if (writeBuffer.Count == 0)
        {
            return null;
        }

        using var ms = new MemoryStream(writeBuffer.ToArray());
        var message = await JsonSerializer.DeserializeAsync<LogMessage<TEvent>>(ms, cancellationToken: cancellationToken);

        return message;
    }

    private string[] GetOrCreateLogSegments()
    {
        var logSegments = Directory.GetFiles(_workingDirectory, $"*{EventLogFileExtension}").ToList();
        var newFilesCount = Math.Max(_topic.Partitions, logSegments.Count) - logSegments.Count;
        if (newFilesCount == 0)
        {
            return logSegments.ToArray();
        }

        var fileNames = logSegments.Select(Path.GetFileNameWithoutExtension).Select(v => int.Parse(v!)).ToArray();
        var maxSegment = fileNames.Length > 0 ? fileNames.Max() : -1;
        var newFiles = Enumerable.Range(maxSegment + 1, newFilesCount).Select(v => Path.Combine(_workingDirectory, $"{v}{EventLogFileExtension}"))
            .ToArray();
        logSegments.AddRange(newFiles);

        return logSegments.ToArray();
    }

    private void InitWorkingDirectory()
    {
        _workingDirectory = Path.Combine(Environment.CurrentDirectory, _topic.Value);
        Directory.CreateDirectory(_workingDirectory);
    }

    private async Task InitCounters(CancellationToken cancellationToken)
    {
        var logSegments = GetOrCreateLogSegments();
        var partitionsCount = Convert.ToByte(logSegments.Length);
        var partitionsMap = Enumerable.Range(0, partitionsCount).Zip(logSegments, (k, v) => new { key = (byte)k, val = v })
            .ToDictionary(v => v.key, v => v.val);
        var initialOffsets = await PrepareInitialOffsets(partitionsMap, cancellationToken);

        _partitionsMap = partitionsMap;
        _partitionsCount = partitionsCount;
        _offsets = new ConcurrentDictionary<byte, ulong>(initialOffsets);
    }

    private Task AssignConsumers(CancellationToken cancellationToken)
    {
        var consumers = _consumers.GroupBy(v=>v.GetGroupName()).ToArray();//.ToDictionary(v=>v.Key, v=>v.ToArray());
        if (consumers.Length == 0)
        {
            return Task.CompletedTask;
        }
        
        foreach (var item in consumers)
        {
            AssignGroupConsumers(item.ToArray());
        }
        
        return Task.CompletedTask;
    }

    private void AssignGroupConsumers(IEventConsumer<TEvent>[] consumers)
    {
        var consumersCount = consumers.Length;
        
        for (byte partition = 0; partition < _partitionsCount; partition++)
        {
            var consumerIndex = partition % consumersCount;
            var logSegmentPath = _partitionsMap[partition];
            var segment = new FileLogSegment(partition, logSegmentPath);
            
            consumers[consumerIndex].AssignSegment(segment);
        }
    }
    
    private async Task<Dictionary<byte, ulong>> PrepareInitialOffsets(Dictionary<byte, string> partitionsMap, CancellationToken cancellationToken)
    {
        var result = new Dictionary<byte, ulong>();

        foreach (var item in partitionsMap)
        {
            var latestMsg = await ReadLatestMessage(item.Value, cancellationToken);
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