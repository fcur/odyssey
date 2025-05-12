using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Producer;

public sealed class EventProducer<TEvent> : IEventProducer<TEvent> where TEvent : class
{
    private readonly IEventBroker<TEvent> _broker;
    private readonly EventProducerSettings _settings;
    private readonly Channel<LogRequest<TEvent>> _channel;
    
    public EventProducer(IEventBroker<TEvent> broker, EventProducerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(settings);
        
        _broker = broker;
        _settings = settings;
        var opt = new BoundedChannelOptions(1) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait};
        _channel= Channel.CreateBounded<LogRequest<TEvent>>(opt);
    }
    
    public ValueTask Publish(LogRequest<TEvent> request, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            if (_channel.Reader.TryRead(out var item))
            {
                var offset = await _broker.LogEvent(item, cancellationToken);
            }
        }
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        _channel.Writer.Complete();
        return Task.CompletedTask;
    }
}