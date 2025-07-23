using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Consumer;

public sealed class EventConsumer<TEvent> : IEventConsumer<TEvent> where TEvent : class
{
    private readonly IEventBroker<TEvent> _broker;
    private readonly IEventConsumerImpl<TEvent> _handler;
    private readonly EventConsumerSettings _settings;
    private readonly Channel<LogRespone<TEvent>> _channel;
    private readonly byte _index;
    private readonly ConcurrentDictionary<byte, LogSegment> _logSegments;

    public EventConsumer(IEventBroker<TEvent> broker, IEventConsumerImpl<TEvent> handler, EventConsumerSettings settings, byte index)
    {
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(settings);
        // create logger

        _broker = broker;
        _settings = settings;
        _handler = handler;
        _index = index;
        _logSegments = new ConcurrentDictionary<byte, LogSegment>();

        var opt = new BoundedChannelOptions(settings.BatchSize) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        _channel = Channel.CreateBounded<LogRespone<TEvent>>(opt);
    }

    public byte GetIndex() => _index;
    public string GetGroupName() => _settings.GroupName;

    public int GetSegmentsCount() => _logSegments.Count;

    public void AssignSegment(LogSegment segment)
    {
        _logSegments.AddOrUpdate(segment.PartitionId, segment, (key, value) => segment);
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        _ = Task.Factory.StartNew(async () => await StartConsumeInternal(cancellationToken), TaskCreationOptions.LongRunning).Unwrap();

        return Task.CompletedTask;
    }

    private async Task StartConsumeInternal(CancellationToken cancellationToken)
    {
        var logSegment = new LogSegment(_index);
        var pullDuration = _settings.PullDuration;
        var batchSize = _settings.BatchSize;

        while (true)
        {
            var cts = new CancellationTokenSource(pullDuration);
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var events = await _broker.PollEvents(logSegment, batchSize, tokenSource.Token);

            foreach (var item in events)
            {
                await _handler.Handle(item, tokenSource.Token);
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
        await _channel.Writer.WriteAsync(response, cancellationToken);
    }
}