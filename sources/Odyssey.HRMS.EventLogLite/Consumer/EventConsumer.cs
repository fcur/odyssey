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
    private readonly Channel<LogResponse<TEvent>> _channel;
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
        _channel = Channel.CreateBounded<LogResponse<TEvent>>(opt);
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
        _ = Task.Factory.StartNew(async () => await StarHandlingInternal(cancellationToken), TaskCreationOptions.LongRunning).Unwrap();

        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task Broadcast(LogResponse<TEvent> response, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(response, cancellationToken);
    }

    private async Task StartConsumeInternal(CancellationToken cancellationToken)
    {
        var logSegment = new LogSegment(_index);

        while (true)
        {
            var cts = new CancellationTokenSource(_settings.PullDuration);
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            var pollRequest = new PollRequest
            {
                BatchSize = _settings.BatchSize,
                TopicName = _settings.TopicName,
                GroupName = _settings.GroupName
            };

            var events = await _broker.PollEvents(pollRequest, logSegment, tokenSource.Token);

            foreach (var item in events)
            {
                item.Metadata[nameof(EventLogBaseSettings.TopicName)] = _settings.TopicName;
                item.Metadata[nameof(EventConsumerSettings.GroupName)] = _settings.GroupName;
                // TODO: to const
                item.Metadata["EventTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                await Broadcast(item, tokenSource.Token);
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private async Task StarHandlingInternal(CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            var item = await _channel.Reader.ReadAsync(cancellationToken);
            await _handler.Handle(item, cancellationToken);
            var commitTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var offsetRequest = new LogOffsetRequest
            {
                Key = new LogOffsetKey(_settings.GroupName, _settings.TopicName, item.PartitionId),
                Value = new LogOffsetValue(item.Offset + 1, commitTimestamp)
            };

            await _broker.Commit(offsetRequest, cancellationToken);
            // TBD: commit
            // if (_channel.Reader.TryRead(out var item))
            // {
            //     var offset = await _broker.LogEvent(item, cancellationToken);
            // }
        }
    }
}