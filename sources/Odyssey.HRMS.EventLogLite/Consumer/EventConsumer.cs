using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Consumer;

public sealed class EventConsumer<TEvent> : IEventConsumer<TEvent> where TEvent : class
{
    private readonly ILogger<EventConsumer<TEvent>> _logger;
    private readonly IEventBroker<TEvent> _broker;
    private readonly IEventConsumerImpl<TEvent> _handler;
    private readonly EventConsumerSettings _settings;
    private readonly Channel<LogResponse<TEvent>> _channel;
    private readonly byte _index;
    private readonly ConcurrentDictionary<byte, LogSegment> _logSegments;
    private ConcurrentDictionary<byte, long> _savedOffsets = null!;

    public EventConsumer(ILogger<EventConsumer<TEvent>> logger,
        IEventBroker<TEvent> broker,
        IEventConsumerImpl<TEvent> handler,
        EventConsumerSettings settings,
        byte index)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(settings);
        // create logger

        _logger = logger;
        _broker = broker;
        _settings = settings;
        _handler = handler;
        _index = index;
        _logSegments = new ConcurrentDictionary<byte, LogSegment>();
        _savedOffsets = new ConcurrentDictionary<byte, long>();

        var opt = new BoundedChannelOptions(5000) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait };
        _channel = Channel.CreateBounded<LogResponse<TEvent>>(opt);
    }

    public byte GetIndex() => _index;
    public string GetGroupName() => _settings.GroupName;

    public int GetSegmentsCount() => _logSegments.Count;

    public void AssignSegment(LogSegment segment)
    {
        _logSegments.AddOrUpdate(segment.PartitionId, segment, (key, value) => segment);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        // TODO: assign offsets for each consumer
        await ReadSavedOffsets(cancellationToken);

        _ = Task.Factory.StartNew(async () => await StartConsumeInternal(cancellationToken), TaskCreationOptions.LongRunning).Unwrap();
        _ = Task.Factory.StartNew(async () => await StarHandlingInternal(cancellationToken), TaskCreationOptions.LongRunning).Unwrap();
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
        var sw = new Stopwatch();
        var scope = ScopeState.Create().WithTopic(_settings.TopicName).WithGroup(_settings.GroupName).With("Index", _index);
        var assignedSegments = _logSegments.Values.ToArray();

        var currentOffsets = new ConcurrentDictionary<byte, long>(_savedOffsets);

        using (_logger.BeginScope(scope.State))
        {
            while (true)
            {
                var cts = new CancellationTokenSource(_settings.PullDuration);
                var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                var targetToken = tokenSource.Token;
                var time = DateTimeOffset.UtcNow;
                var requestId = Guid.CreateVersion7(time);

                var pollRequest = new PollRequest
                {
                    BatchSize = _settings.BatchSize,
                    TopicName = _settings.TopicName,
                    GroupName = _settings.GroupName,
                    RequestId = requestId,
                    OccuredAt = time
                };

                _logger.LogDebug("Pulling is being started, RequestId: {RequestId}", requestId);

                sw.Start();
                var tasks = assignedSegments.Select(segment => _broker.PollEvents(pollRequest, segment, currentOffsets[segment.PartitionId], targetToken));
                var results = await Task.WhenAll(tasks);
                var events = results.SelectMany(v => v).ToArray();
                sw.Stop();

                _logger.LogDebug("Pulling completed, results: {Count}, RequestId: {RequestId}, Elapsed: {Elapsed} ms", events.Length, requestId, sw.ElapsedMilliseconds);

                foreach (var item in events)
                {
                    // TODO: to const
                    item.Metadata["TopicName"] = _settings.TopicName;
                    item.Metadata["GroupName"] = _settings.GroupName;
                    item.Metadata["EventTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    await Broadcast(item, targetToken);

                    currentOffsets.TryUpdate(item.PartitionId, item.Offset + 1, item.Offset);
                }

                var pullingPause = _settings.PullDuration - sw.Elapsed;
                if (events.Length == 0 && pullingPause > TimeSpan.Zero && !targetToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Pause pulling for {PullingPause}", pullingPause);
                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    await Task.Delay(pullingPause, targetToken);
                }

                sw.Reset();
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

            // TODO: skip commit according to configuration

            var time = DateTimeOffset.UtcNow;
            var requestId = Guid.CreateVersion7(time);

            var offsetRequest = new LogOffsetRequest
            {
                Key = new LogOffsetKey(_settings.GroupName, _settings.TopicName, item.PartitionId),
                Value = new LogOffsetValue(item.Offset + 1, time.ToUnixTimeMilliseconds()),
                Metadata = new Dictionary<string, object> { { "Key", item.Key ?? string.Empty } },
                RequestId = requestId,
                OccuredAt = time
            };

            var scope = ScopeState.Create().WithOffset(item.Offset).WithTopic(_settings.TopicName)
                .WithGroup(_settings.GroupName).WithPartition(item.PartitionId).WithRequestId(requestId);

            using (_logger.BeginScope(scope.State))
            {
                _logger.LogDebug("Offset committing is being started");

                await _broker.Commit(offsetRequest, cancellationToken);

                _savedOffsets.AddOrUpdate(offsetRequest.Key.PartitionId, offsetRequest.Value.NextMsgOffset,
                    (key, value) => offsetRequest.Value.NextMsgOffset);

                _logger.LogDebug("Offset committing finished");
            }
        }
    }

    private async Task ReadSavedOffsets(CancellationToken cancellationToken)
    {
        var keys = _logSegments.Keys.Select(v => new LogOffsetKey(_settings.GroupName, _settings.TopicName, v));

        var tasks = keys.Select(v => _broker.ReadSavedOffset(new ReadOffsetRequest { Key = v }, cancellationToken));
        var results = await Task.WhenAll(tasks);

        foreach (var item in results)
        {
            _savedOffsets.AddOrUpdate(item.Key.PartitionId, item.Value.NextMsgOffset, (key, value) => item.Value.NextMsgOffset);
        }
    }
}