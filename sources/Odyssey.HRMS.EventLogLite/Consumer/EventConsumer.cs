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
    private ConcurrentDictionary<byte, ulong> _offsets = null!;

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
        _offsets = new ConcurrentDictionary<byte, ulong>();

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
        var sw = new Stopwatch();
        var scope = ScopeState.Create().WithTopic(_settings.TopicName).WithGroup(_settings.GroupName).With("Index",_index);
        var assignedSegments = _logSegments.Values.ToArray();

        using (_logger.BeginScope(scope.State))
        {
            while (true)
            {
                var cts = new CancellationTokenSource(_settings.PullDuration);
                var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
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
                var tasks = assignedSegments.Select(v => _broker.PollEvents(pollRequest, v, tokenSource.Token));
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

                    await Broadcast(item, tokenSource.Token);
                }

                var pullingPause = _settings.PullDuration - sw.Elapsed;

                if (pullingPause > TimeSpan.Zero)
                {
                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    await Task.Delay(pullingPause, cancellationToken);
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
            
            var time = DateTimeOffset.UtcNow;
            var requestId = Guid.CreateVersion7(time);
            
            var offsetRequest = new LogOffsetRequest
            {
                Key = new LogOffsetKey(_settings.GroupName, _settings.TopicName, item.PartitionId),
                Value = new LogOffsetValue(item.Offset + 1, time.ToUnixTimeMilliseconds()),
                Metadata = new Dictionary<string, object>() { { "Key", item.Key ?? "NONE" } },
                RequestId = requestId,
                OccuredAt = time
            };

            var scope = ScopeState.Create().WithOffset(item.Offset).WithTopic(_settings.TopicName)
                .WithGroup(_settings.GroupName).WithPartition(item.PartitionId).WithRequestId(requestId);

            using (_logger.BeginScope(scope.State))
            {
                _logger.LogDebug("Offset committing is being started");
                
                await _broker.Commit(offsetRequest, cancellationToken);
            
                _logger.LogDebug("Offset committing finished");
            }
        }
    }
}