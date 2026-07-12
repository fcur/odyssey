using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Producer;

public sealed class EventProducer<TEvent> : IDisposable, IEventProducer<TEvent> where TEvent : class
{
    private readonly ILogger<EventProducer<TEvent>> _logger;
    private readonly IEventProducerBroker _broker;
    private readonly EventProducerSettings _settings;
    private readonly Channel<LogRequest<TEvent>> _channel;

    public EventProducer(ILogger<EventProducer<TEvent>> logger, IEventProducerBroker broker, EventProducerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(settings);
        
        _logger = logger;
        _broker = broker;
        _settings = settings;
        
        var opt = new BoundedChannelOptions(1) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait, 
            // AllowSynchronousContinuations = true 
        };
        _channel = Channel.CreateBounded<LogRequest<TEvent>>(opt);
    }
    
    // TBD: publish batch
    public ValueTask Publish(LogRequest<TEvent> request, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        _ = Task.Factory.StartNew(() => StartConsumeProducedEventsInternal(cancellationToken), TaskCreationOptions.LongRunning).Unwrap();
        // Task.Run(() =>  StartConsumeProducedEventsInternal(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task StartConsumeProducedEventsInternal(CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            var item = await _channel.Reader.ReadAsync(cancellationToken);
            var offset = await _broker.LogEvent(item, cancellationToken);
            // if (_channel.Reader.TryRead(out var item))
            // {
            //     var offset = await _broker.LogEvent(item, cancellationToken);
            // }
        }
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        _channel.Writer.Complete();
        return Task.CompletedTask;
    }

    public EventProducerSettings GetSettings()
    {
        return _settings;
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
    }

}