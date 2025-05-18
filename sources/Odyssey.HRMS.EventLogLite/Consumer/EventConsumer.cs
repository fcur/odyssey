using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Consumer;

public sealed class EventConsumer<TEvent> : IEventConsumer<TEvent> where TEvent : class
{
    private readonly IEventConsumerImpl<TEvent> _handler;
    private readonly EventConsumerSettings _settings;
    private readonly Channel<LogRespone<TEvent>> _channel;

    public EventConsumer(IEventConsumerImpl<TEvent> handler, EventConsumerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _handler = handler;
        
        var opt = new BoundedChannelOptions(1000) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        _channel = Channel.CreateBounded<LogRespone<TEvent>>(opt);
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        _ = Task.Factory.StartNew(async () => await StartConsumeInternal(cancellationToken), TaskCreationOptions.LongRunning).Unwrap();
        
        return Task.CompletedTask;
    }

    private async Task StartConsumeInternal(CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            if (_channel.Reader.TryRead(out var item))
            {
                await _handler.Handle(item, cancellationToken);
                // TBD: commit
            }
        }
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
    
    public async Task Broadcast(LogRespone<TEvent> response, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(response,  cancellationToken);
    }
}