using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Base;

// TODO: add compression
// https://github.com/cocowalla/serilog-sinks-file-gzip
public sealed class FileEventLogBroker<TEvent> : IEventBroker<TEvent> where TEvent : class
{
    private readonly EventLogTopic _topic;
    private string _workingDirectory;
    private readonly Channel<LogRespone<TEvent>> _mainChannel;
    private readonly ConcurrentQueue<IEventConsumer<TEvent>> _consumerChannels;
    private byte _partitionsCount = 0;
    private int _tempPartition = 0;
    private ConcurrentDictionary<byte, ulong> _offsets;
    private Dictionary<byte, string> _partitionsMap;

    public FileEventLogBroker(EventLogTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        _topic = topic;
        _consumerChannels = [];

        var opt = new BoundedChannelOptions(1000) { SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        _mainChannel = Channel.CreateBounded<LogRespone<TEvent>>(opt);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        // TODO: add rebalance
        // NOT possible to decrease partitions count

        InitWorkingDirectory();
        await InitCounters(cancellationToken);

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

    public async Task<EventLogOffset> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default)
    {
        var partitionId = GetPartition(request);
        var logFilePath = _partitionsMap[partitionId];
        var logMessage = LogMessage<TEvent>.Create(request);

        if (_offsets.TryGetValue(partitionId, out var offsetResult))
        {
        }

        await using (var fs = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.Write))
        {
            fs.Seek(0, SeekOrigin.End);
            await JsonSerializer.SerializeAsync(fs, logMessage, new JsonSerializerOptions { WriteIndented = false }, cancellationToken);
        }

        var fileInfo = new FileInfo(logFilePath);
        var offset = new EventLogOffset(fileInfo.Length);

        var response = new LogRespone<TEvent>
        {
            Key = request.Key,
            Payload = request.Payload,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
            Offset = offset.Value, // TBD unique number inside partition file
            PartitionId = partitionId
        };

        await _mainChannel.Writer.WriteAsync(response, cancellationToken);

        return offset;
    }

    public void Join(IEventConsumer<TEvent> consumer, CancellationToken cancellationToken = default)
    {
        _consumerChannels.Enqueue(consumer);
    }

    private byte GetPartition(LogRequest<TEvent> request)
    {
        if (request.PartitionId.HasValue)
        {
            return request.PartitionId.Value!;
        }

        if (!string.IsNullOrEmpty(request.Key))
        {
            return Convert.ToByte(request.Key.GetHashCode() % _partitionsCount);
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
        var writeBuffer = new Stack<byte>();
        var newLineCode = Convert.ToByte('\n');
        
        await using (var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read))
        {
            if (fs.Length == 0)
            {
                return null;
            }
            
            var offset = -1;
            
            fs.Seek(offset, SeekOrigin.End);
            var readBuffer = new byte[1];
            while (fs.Position > 0)
            {
                await fs.ReadExactlyAsync(readBuffer, 0, 1, cancellationToken);

                if (readBuffer[0] == newLineCode)
                {
                    break;
                }

                writeBuffer.Push(readBuffer[0]);
                fs.Seek(--offset, SeekOrigin.Current);
            }
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
        const string logFileExtension = ".log";
        
        var logSegments = Directory.GetFiles(_workingDirectory, $"*{logFileExtension}").ToList();
        var newFilesCount = Math.Max(_topic.Partitions, logSegments.Count) - logSegments.Count;
        if (newFilesCount == 0)
        {
            return logSegments.ToArray();
        }

        var fileNames = logSegments.Select(Path.GetFileNameWithoutExtension).Select(v => int.Parse(v!)).ToArray();
        var maxSegment = fileNames.Length > 0 ? fileNames.Max() : -1;
        var newFiles = Enumerable.Range(maxSegment + 1, newFilesCount).Select(v => Path.Combine(_workingDirectory, $"{v}{logFileExtension}")).ToArray();
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