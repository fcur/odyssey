using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventBroker<TEvent> where TEvent : class
{
    Task<EventLogOffset> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default);

    void Join(IEventConsumer<TEvent> consumer, CancellationToken cancellationToken = default);
}

// TODO: add compression
// https://github.com/cocowalla/serilog-sinks-file-gzip
public sealed class FileEventLogBroker<TEvent> : IEventBroker<TEvent> where TEvent : class
{
    private readonly string _workingDirectory;
    private readonly Channel<LogRespone<TEvent>> _mainChannel;
    private readonly ConcurrentQueue<IEventConsumer<TEvent>> _consumerChannels;
    private readonly byte _partitionsCount = 0;
    private int _tempPartition = 0;
    private readonly ConcurrentDictionary<byte, ulong> _offsets;

    public FileEventLogBroker(EventLogTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        _workingDirectory = Path.Combine(Environment.CurrentDirectory, topic.Value);
        _partitionsCount = topic.Partitions;
        _offsets = new ConcurrentDictionary<byte, ulong>(Enumerable.Range(0, _partitionsCount).ToDictionary(v => (byte)v, k => 0UL));
        _consumerChannels = [];

        var opt = new BoundedChannelOptions(1000) { SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        _mainChannel = Channel.CreateBounded<LogRespone<TEvent>>(opt);
    }

    private string ReadLatestMessage(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var offset = -1;

        fs.Seek(offset, SeekOrigin.End);
        byte[] buffer = new byte[1];
        var sb = new StringBuilder();

        while (fs.Position > 0)
        {
            fs.ReadExactly(buffer, 0, 1);
            char c = (char)buffer[0];

            if (c == '\n')
            {
                break;
            }
            
            sb.Insert(0, c);
            offset--;
            fs.Seek(offset, SeekOrigin.Current);
        }

        return sb.ToString();
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_workingDirectory);

        var logSegments = Directory.GetFiles(_workingDirectory, "*.log");
        var latestMessages = logSegments.Select(ReadLatestMessage).ToArray();


        while (await _mainChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            if (_mainChannel.Reader.TryRead(out var item))
            {
                // TBD: publish to all consumers
            }
        }
    }

    public async Task<EventLogOffset> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default)
    {
        var partitionId = GetPartition(request);
        var fileName = GetFileName(request, partitionId);
        var logFilePath = Path.Combine(_workingDirectory, fileName);
        var logMessage = LogMessage<TEvent>.Create(request);

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

    private string GetFileName(LogRequest<TEvent> request, byte partitionId)
    {
        var template = $"{{0}}{EventLogSettings.LogFileExtension}";

        return string.Format(template, partitionId);
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
}

public sealed record EventLogOffset(long Value);

public sealed record EventLogTopic(string Value, byte Partitions);