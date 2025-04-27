using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Odyssey.HRMS.EventLogLite.Producer;

// TODO: add compression
// https://github.com/cocowalla/serilog-sinks-file-gzip
public sealed class EventProducer<TEvent>(EventProducerSettings settings) : IEventProducer<TEvent> where TEvent : class
{
    private readonly BlockingCollection<LogRequest<TEvent>> _queue = new(new ConcurrentQueue<LogRequest<TEvent>>());
    private readonly string _topicWorkingDirectory = settings.GetTopicWorkingDirectory();

    public Task Publish(LogRequest<TEvent> request, CancellationToken cancellationToken = default)
    {
        _queue.Add(request, cancellationToken);
        return Task.CompletedTask;
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        EnsureTopicDirectoryExists();

        var infinityTimeout = -1;

        _ = Task.Factory.StartNew<Task>(async () =>
        {
            while (!_queue.IsAddingCompleted)
            {
                if (_queue.TryTake(out var item, infinityTimeout, cancellationToken))
                {
                    var offset = await LogEvent(item, cancellationToken);
                    
                    
                    
                }
            }
        }, TaskCreationOptions.LongRunning);

        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        _queue.CompleteAdding();
        return Task.CompletedTask;
    }

    private async Task<long> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default)
    {
        var fileName = GetFileName(request);
        var logFilePath = Path.Combine(_topicWorkingDirectory, fileName);
        var logMessage = LogMessage<TEvent>.Create(request);

        await using (var fs = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.Write))
        {
            fs.Seek(0, SeekOrigin.End);
            await JsonSerializer.SerializeAsync(fs, logMessage, new JsonSerializerOptions { WriteIndented = false }, cancellationToken);
        }
        
        var fileInfo = new FileInfo(logFilePath);

        return fileInfo.Length;
    }

    public void EnsureTopicDirectoryExists()
    {
        Directory.CreateDirectory(_topicWorkingDirectory);
    }

    private string GetFileName(LogRequest<TEvent> request)
    {
        var template = $"{request.Key}-{{0}}{EventLogSettings.LogFileExtension}";

        return string.Format(template, request.PartitionId);
    }
}