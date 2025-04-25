using System.Collections.Concurrent;

namespace Odyssey.HRMS.EventLogLite;

public sealed class EventLogSettings
{
    public const string ConfigurationSectionName = "EventLogging";
    public const string ProducerSectionName = "Producer";
    public const string ConsumerSectionName = "Consumer";
}

public interface IEventProducer
{
    Task Start(CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
}

public interface IEventProducer<in TEvent> : IEventProducer where TEvent : class
{
    Task Publish(TEvent @event, CancellationToken cancellationToken);
}

public interface IEventConsumer
{
}

public interface IEventConsumer<TEvent> : IEventConsumer where TEvent : class
{
}

public sealed class EventProducer<TEvent>(EventProducerSettings settings) : IEventProducer<TEvent> where TEvent : class
{
    private readonly EventProducerSettings _settings = settings;
    private readonly BlockingCollection<TEvent> _queue = new(new ConcurrentQueue<TEvent>());

    public Task Publish(TEvent @event, CancellationToken cancellationToken = default)
    {
        _queue.Add(@event, cancellationToken);
        return Task.CompletedTask;
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        var infinityTimeout = -1;

        _ = Task.Factory.StartNew<Task>(async () =>
        {
            while (!_queue.IsAddingCompleted)
            {
                if (_queue.TryTake(out var item, infinityTimeout, cancellationToken))
                {
                    await LogEvent(item, cancellationToken);
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

    private async Task LogEvent(TEvent item, CancellationToken cancellationToken = default)
    {
        // offset = file.length
        await Task.Delay(150, cancellationToken);
    }
    
}

public sealed class EventProducerSettings
{
    public string TopicName { get; set; } = null!;
}

public sealed class EventConsumer<TEvent>(EventConsumerSettings settings) : IEventConsumer<TEvent> where TEvent : class
{
    private readonly EventConsumerSettings _settings = settings;


    public Task Start()
    {
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        return Task.CompletedTask;
    }
}

public sealed class EventConsumerSettings
{
}