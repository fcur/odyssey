using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventBroker<TEvent> where TEvent : class
{
    Task<EventLogOffset> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default);
    
    void Join(IEventConsumer<TEvent>  consumer, CancellationToken cancellationToken = default);
}

// TODO: add compression
// https://github.com/cocowalla/serilog-sinks-file-gzip
public sealed class FileEventLogBroker<TEvent> : IEventBroker<TEvent> where TEvent : class
{
    private readonly string _workingDirectory;
    private readonly Channel<LogRespone<TEvent>> _mainChannel;
    private readonly ConcurrentQueue<IEventConsumer<TEvent>> _consumers;

    public FileEventLogBroker(EventLogTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        _workingDirectory = Path.Combine(Environment.CurrentDirectory, topic.Value);
        Directory.CreateDirectory(_workingDirectory);

        var opt = new BoundedChannelOptions(1000) { SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        _mainChannel = Channel.CreateBounded<LogRespone<TEvent>>(opt);
        _consumers = [];
    }
    
    public async Task<EventLogOffset> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default)
    {
        var fileName = GetFileName(request);
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
            PartitionId = 0 // TBD
        };
        
        await _mainChannel.Writer.WriteAsync(response, cancellationToken);

        return offset;
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        while (await _mainChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            if (_mainChannel.Reader.TryRead(out var item))
            {
                
                // TBD: publish to all consumers
                
                
                
            }
        }
    }
    
    public void Join(IEventConsumer<TEvent> consumer, CancellationToken cancellationToken = default)
    {
        _consumers.Enqueue(consumer);
    }

    private string GetFileName(LogRequest<TEvent> request)
    {
        var template = $"{request.Key}-{{0}}{EventLogSettings.LogFileExtension}";

        return string.Format(template, request.PartitionId);
    }
}

public sealed record EventLogOffset(long Value);

public sealed record EventLogTopic(string Value);