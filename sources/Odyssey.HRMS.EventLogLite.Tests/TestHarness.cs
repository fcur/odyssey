using Microsoft.Extensions.Logging;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
public sealed record TestHarnessSettings
{
    public required EventLogTopic Topic { get; init; }
    public required EventBrokerSettings BrokerSettings { get; init; }
    public required EventProducerSettings ProducerSettings { get; init; }
    public required EventConsumerSettings[] ConsumerSettings { get; init; }
    public required Dictionary<byte, long> LatestOffsets { get; init; }
    public required Dictionary<LogOffsetKey, long> SavedOffsets { get; init; }
    public long MaxOffset;
}

[ExcludeFromCodeCoverage]
public sealed class TestHarness<TEvent> where TEvent : class, new()
{
    private readonly FileEventLogBroker<TEvent> _broker;
    private readonly EventProducer<TEvent> _producer;
    private readonly IReadOnlyCollection<EventConsumer<TEvent>> _consumers;
    private readonly TestHarnessSettings  _settings;
    
    private readonly ConcurrentDictionary<string, ConcurrentQueue<LogResponse<TEvent>>> _unhandledEvents = new();
    private readonly ConcurrentBag<LogResponse<TEvent>> _loggedEvents = new();
    private readonly ConcurrentBag<LogResponse<TEvent>> _handledEvents = new();
    private readonly ConcurrentQueue<LogOffsetRequest> _committedOffsets = new();

    private long _pollingAttemptsCountCounter = 0;
    private long _polledEventsCountCounter = 0;
    
    public long PolledEventsCount => _polledEventsCountCounter;
    public long PollingAttemptsCount => _pollingAttemptsCountCounter;
    public long LoggedEventsCount => _loggedEvents.Count;
    
    public ConsumerAssigmentState[] GetAssigmentStates() => _consumers.Select(v => v.GetConsumerAssigmentState()).ToArray();
    
    public TestHarness(TestHarnessSettings settings, ILoggerFactory loggerFactory)
    {
        _settings = settings;
        
        var brokerLogger = loggerFactory.CreateLogger<FileEventLogBroker<TEvent>>();
        var producerLogger = loggerFactory.CreateLogger<EventProducer<TEvent>>();
        var logger = loggerFactory.CreateLogger<EventConsumer<TEvent>>();
        
        var eventLoggerMock = PrepareEventLogger(settings.LatestOffsets);

        _broker = new FileEventLogBroker<TEvent>(brokerLogger, settings.BrokerSettings, eventLoggerMock.Object, settings.Topic);
        _producer = new EventProducer<TEvent>(producerLogger, _broker, settings.ProducerSettings);
        _consumers = PrepareTopicConsumers(logger, settings.ConsumerSettings);
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        await _broker.Start(cancellationToken);
        await _producer.Start(cancellationToken);
        var consumers = _consumers.Select(v => v.Start(cancellationToken)).ToArray();
        await Task.WhenAll(consumers);
    }

    public async Task Publish(LogRequest<TEvent>[] requests, CancellationToken cancellationToken)
    {
        if (!requests.Any())
        {
            return;
        }
        
        foreach (var request in requests)
        {
            await _producer.Publish(request, cancellationToken);
        }

        await Task.Delay(100, cancellationToken);
    }

    public LogResponse<TEvent>[] GetLoggerEvents()
    {
        return _loggedEvents.ToArray();
    }
    
    public LogResponse<TEvent>? GetLoggerEvent(Func<LogResponse<TEvent>, bool> predicate)
    {
        return _loggedEvents.FirstOrDefault( predicate);
    }
    
    private IReadOnlyCollection<EventConsumer<TEvent>> PrepareTopicConsumers(ILogger<EventConsumer<TEvent>> logger, EventConsumerSettings[] settings)
    {
        if (settings.Length == 0)
        {
            return [];
        }

        var consumers = new List<EventConsumer<TEvent>>();

        foreach (var consumerSettings in settings)
        {
            _unhandledEvents.AddOrUpdate(consumerSettings.GroupName, new ConcurrentQueue<LogResponse<TEvent>>(),
                (s, stack) => new ConcurrentQueue<LogResponse<TEvent>>());

            for (byte i = 0; i < consumerSettings.ReplicaCount; i++)
            {
                var consumerImplMock = new Mock<IEventConsumerImpl<TEvent>>();
                consumerImplMock.Setup(v => v.Handle(It.IsAny<LogResponse<TEvent>>(), It.IsAny<CancellationToken>()))
                    .Callback<LogResponse<TEvent>, CancellationToken>((logResponse, _) => HandledEventResponse(logResponse))
                    .Returns(Task.CompletedTask);

                var consumer = new EventConsumer<TEvent>(logger, _broker, consumerImplMock.Object, consumerSettings, i);
                _broker.Join(consumer);
                consumers.Add(consumer);
            }
        }

        return consumers.ToArray();
    }

    private Mock<IFileEventLogger> PrepareEventLogger(Dictionary<byte, long> latestOffsets)
    {
        var eventLoggerMock = new Mock<IFileEventLogger>();

        foreach (var item in latestOffsets)
        {
            eventLoggerMock.Setup(v =>
                    v.ReadLastMessage<TEvent>(It.Is<FileLogSegment>(s => s.PartitionId == item.Key), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LogMessage<TEvent>() { Key = Guid.NewGuid().ToString("D"), Offset = item.Value });
        }

        eventLoggerMock.Setup(v => v.Write<TEvent>(It.IsAny<LogMessage<TEvent>>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogMessage<TEvent> msg, FileLogSegment segment,CancellationToken _) => new PositionPair(0,0))
            .Callback<LogMessage<TEvent>, FileLogSegment, CancellationToken>((logMessage, segment, _) => SaveLoggedEvent(logMessage, segment));

        eventLoggerMock.Setup(v =>
                v.Poll<TEvent>(It.IsAny<PollRequest>(), It.IsAny<FileLogSegment>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((PollRequest request, FileLogSegment segment, long offset, CancellationToken _) => PreparePollResults(request, segment, offset))
            .Callback<PollRequest, FileLogSegment, long, CancellationToken>((request, segment, offset, _) =>
                HandlePollRequest(request, segment, offset));

        eventLoggerMock.Setup(v => v.Commit(It.IsAny<LogOffsetRequest>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogOffsetRequest request, FileLogSegment segment,CancellationToken _) => new PositionPair(0,0))
            .Callback<LogOffsetRequest, FileLogSegment, CancellationToken>((request, segment, _) => HandleCommitedEvent(request, segment));

        eventLoggerMock.Setup(v => v.ReadSavedOffset(It.IsAny<LogOffsetKey>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()))
            .Returns((LogOffsetKey key, FileLogSegment segment, CancellationToken _) => PrepareOffsetResults(key, segment));

        return eventLoggerMock;
    }

    private void HandledEventResponse(LogResponse<TEvent> responseMessage)
    {
        _handledEvents.Add(responseMessage);

        // var groupName = responseMessage.Metadata["GroupName"].ToString();
        // return;

        // var unhandledEventsContainer = _unhandledEvents[groupName!];
        //
        // _ = unhandledEventsContainer.TryDequeue(out var msg);
        // if (responseMessage.Offset != msg!.Offset)
        // {
        //     throw new Exception($"Offset {responseMessage.Offset} does not match offset {msg.Offset}");
        // }
    }

    private IAsyncEnumerable<LogMessage<TEvent>> PreparePollResults(PollRequest request, FileLogSegment segment, long offset)
    {
        if (offset > _settings.MaxOffset)
        {
            return Array.Empty<LogMessage<TEvent>>().ToAsyncEnumerable();
        }

        var batchSize = request.BatchSize + offset > _settings.MaxOffset ? _settings.MaxOffset - offset : request.BatchSize;

        // var fixture = new Fixture();
        // fixture.Create<TEvent>() with { Skipped = true };
        var dumbMsg = new TEvent();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var result = Enumerable.Range(0, (int)batchSize).Select(v => new LogMessage<TEvent>
        {
            Key = string.Empty,
            Payload = dumbMsg,
            Metadata = new Dictionary<string, object>(),
            Offset = offset + v,
            Timestamp = timestamp
        }).ToDictionary(v => v.Offset, v => v);

        var minOffset = offset;
        var maxOffset = offset + batchSize;

        if (_unhandledEvents.TryGetValue(request.GroupName, out var unhandledEvents)
            && !unhandledEvents.IsEmpty)
        {
            var foundEvents = unhandledEvents.Where(v => v.PartitionId == segment.PartitionId
                                                         && v.Offset >= minOffset
                                                         && v.Offset <= maxOffset).ToArray();

            foreach (var item in foundEvents)
            {
                result[item.Offset] = new LogMessage<TEvent>
                {
                    Payload = item.Payload,
                    Key = item.Key!,
                    Offset = item.Offset,
                    Timestamp = item.Timestamp.ToUnixTimeMilliseconds(),
                    Metadata = item.Metadata
                };
            }
        }

        Interlocked.Add(ref _polledEventsCountCounter, result.Count);
        return result.Values.ToAsyncEnumerable();
    }

    private Task<LogOffsetMessage> PrepareOffsetResults(LogOffsetKey key, FileLogSegment segment)
    {
        var result = _settings.SavedOffsets.TryGetValue(key, out var nextOffset)
            ? new LogOffsetMessage { Key = key, Value = new LogOffsetValue(nextOffset, 0) }
            : LogOffsetMessage.CreateNew(key);

        return Task.FromResult(result);
    }

    private void SaveLoggedEvent(LogMessage<TEvent> logMessage, FileLogSegment segment)
    {
        if (logMessage.Key == string.Empty)
        {
            return;
        }

        var response = new LogResponse<TEvent>
        {
            Key = logMessage.Key,
            Payload = logMessage.Payload,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
            Offset = logMessage.Offset,
            PartitionId = segment.PartitionId,
            Metadata = new Dictionary<string, object>()
        };

        _loggedEvents.Add(response);

        foreach (var key in _unhandledEvents.Keys)
        {
            _unhandledEvents[key].Enqueue(response);
        }
    }

    private void HandleCommitedEvent(LogOffsetRequest request, FileLogSegment segment)
    {
        var msgKey = request.Metadata["Key"].ToString();
        if (msgKey == string.Empty)
        {
            return;
        }

        _committedOffsets.Enqueue(request);

        if (!_unhandledEvents.TryGetValue(request.Key.ConsumerGroupName, out var unhandledEvents)
            || unhandledEvents.IsEmpty)
        {
            return;
        }

        _ = unhandledEvents.TryDequeue(out var response);
    }

    private void HandlePollRequest(PollRequest request, FileLogSegment segment, long offset)
    {
        Interlocked.Increment(ref _pollingAttemptsCountCounter);
    }

    private IReadOnlyCollection<LogResponse<TEvent>> GetLoggedEvents(string key)
    {
        return _loggedEvents.Where(v => v.Key == key).ToArray();
    }
}