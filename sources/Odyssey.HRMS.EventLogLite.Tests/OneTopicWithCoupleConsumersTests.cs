using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;
using Odyssey.HRMS.EventLogLite.Tests.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Xunit.Abstractions;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
public sealed class OneTopicWithCoupleConsumersTests
{
    private const string TopicName = "test";
    private const byte Partitions = 5;
    private const uint FileSizeLimitBytes = 1024 * 1024;
    private const int BatchSize = 100;
    private const string Group1Name = "Group1";
    private const string Group2Name = "Group2";
    private const string EventName = "Event1";
    private static readonly TimeSpan PullDuration = TimeSpan.FromSeconds(1);

    private readonly ILoggerFactory  _loggerFactory;
    private readonly Mock<IFileEventLogger> _eventLoggerMock;
    private readonly FileEventLogBroker<TestEvent> _broker;
    private readonly EventProducer<TestEvent> _producer;
    private readonly List<EventConsumer<TestEvent>> _consumers = [];
    private readonly ConcurrentBag<LogResponse<TestEvent>> _loggedEvents = new();
    private readonly ConcurrentBag<LogResponse<TestEvent>> _handledEvents = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<LogResponse<TestEvent>>> _unhandledEvents = new();
    private readonly Lock _unhandledEventsLock = new();
    private readonly ITestOutputHelper _outputHelper;

    private readonly Dictionary<byte, uint> _initialOffsets = new()
    {
        { 0, 33 },
        { 1, 44 },
        { 2, 24 },
        { 3, 29 },
        { 4, 39 }
    };

    public OneTopicWithCoupleConsumersTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _loggerFactory = LoggerFactory.Create(builder => { builder
            .AddProvider(new XunitLoggerProvider(outputHelper))
            .SetMinimumLevel(LogLevel.Trace); });
        _eventLoggerMock = PrepareEventLogger();
        
        var brokerLogger = _loggerFactory.CreateLogger<FileEventLogBroker<TestEvent>>();
        var producerLogger = _loggerFactory.CreateLogger<EventProducer<TestEvent>>();
        
        var topic = new EventLogTopic(TopicName, Partitions);
        var brokerSettings = new EventBrokerSettings() { TopicName = "__consumer_offsets", Partitions = 5 };
        _broker = new FileEventLogBroker<TestEvent>(brokerLogger, brokerSettings, _eventLoggerMock.Object, topic);
        
        var producerSettings = new EventProducerSettings { FileSizeLimitBytes = FileSizeLimitBytes, TopicName = TopicName, Partitions = Partitions };
        _producer = new EventProducer<TestEvent>(producerLogger, _broker, producerSettings);

        PrepareTopicConsumers(Group1Name, 5);
        PrepareTopicConsumers(Group2Name, 1);
    }

    [Fact]
    public async Task Should_Assign_Consumers()
    {
        await _broker.Start();

        using var scope = new AssertionScope();
        _consumers.Count.Should().Be(6);

        EnsureConsumerAssigment(_consumers[0], 0, Group1Name, 1);
        EnsureConsumerAssigment(_consumers[1], 1, Group1Name, 1);
        EnsureConsumerAssigment(_consumers[2], 2, Group1Name, 1);
        EnsureConsumerAssigment(_consumers[3], 3, Group1Name, 1);
        EnsureConsumerAssigment(_consumers[4], 4, Group1Name, 1);
        EnsureConsumerAssigment(_consumers[5], 0, Group2Name, 5);
    }

    [Theory, AutoData]
    public async Task Should_PublishTo_Partition3(TestEvent payload)
    {
        var key = "0342b673-b710-4a52-a60d-5993ae42d2ad";
        var ct = CancellationToken.None;
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };

        await Start(ct);
        await Publish(ct, request);

        var loggedMessage = GetLoggedEvents(key).FirstOrDefault();

        using var scope = new AssertionScope();

        _eventLoggerMock.Verify(v => v.Write(It.IsAny<LogMessage<TestEvent>>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()), Times.Once);
        loggedMessage.Should().NotBeNull();
        loggedMessage?.Key.Should().Be(key);
        loggedMessage?.Payload.Should().Be(payload);
        loggedMessage?.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        loggedMessage?.Offset.Should().Be(_initialOffsets[3] + 1);
        loggedMessage?.PartitionId.Should().Be(3);
    }

    [Theory, AutoData]
    public async Task Should_StartConsume_FromBeginning(TestEvent payload1, Guid key1, TestEvent payload2, Guid key2)
    {
        var cts = new CancellationTokenSource();
        var request1 = new LogRequest<TestEvent> { Key = key1.ToString("D"), Payload = payload1 };
        var request2 = new LogRequest<TestEvent> { Key = key2.ToString("D"), Payload = payload2 };

        await Start(cts.Token);
        await Publish(cts.Token, request1, request2);

        await Task.Delay(3000, CancellationToken.None);
        
        using var scope = new AssertionScope();
        
        _eventLoggerMock.Verify(v => v.Write(It.IsAny<LogMessage<TestEvent>>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _eventLoggerMock.Verify(v => v.Poll<TestEvent>(It.IsAny<PollRequest>(), It.IsAny<FileLogSegment>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
        _eventLoggerMock.Verify(v => v.Commit(It.IsAny<LogOffsetRequest>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()), Times.Exactly(4));

        _loggedEvents.Should().Contain(v => v.Key == request1.Key);
    }

    [Theory, AutoData]
    public async Task Should_StartConsume_FromKnownOffsets(TestEvent payload1, Guid key1, TestEvent payload2, Guid key2)
    {
        var cts = new CancellationTokenSource();
        var request1 = new LogRequest<TestEvent> { Key = key1.ToString("D"), Payload = payload1 };
        var request2 = new LogRequest<TestEvent> { Key = key2.ToString("D"), Payload = payload2 };

        await Start(cts.Token);
        await Publish(cts.Token, request1, request2);
    }

    private void PrepareTopicConsumers(string groupName, byte replicaCount)
    {
        _unhandledEvents.AddOrUpdate(groupName, new ConcurrentQueue<LogResponse<TestEvent>>(),
            (s, stack) => new ConcurrentQueue<LogResponse<TestEvent>>());

        var consumerSettings = new EventConsumerSettings(groupName)
        {
            TopicName = TopicName,
            BatchSize = BatchSize,
            ReplicaCount = replicaCount,
            EventName = EventName,
            PullDuration = PullDuration
        };

        for (byte i = 0; i < replicaCount; i++)
        {
            var consumerImplMock = new Mock<IEventConsumerImpl<TestEvent>>();
            consumerImplMock.Setup(v => v.Handle(It.IsAny<LogResponse<TestEvent>>(), It.IsAny<CancellationToken>()))
                .Callback<LogResponse<TestEvent>, CancellationToken>((logResponse, _) => HandledEventResponse(logResponse))
                .Returns(Task.CompletedTask);
                
            var logger = _loggerFactory.CreateLogger<EventConsumer<TestEvent>>();
            
            var consumer = new EventConsumer<TestEvent>(logger, _broker, consumerImplMock.Object, consumerSettings, i);
            _broker.Join(consumer);
            _consumers.Add(consumer);
        }
    }

    private Mock<IFileEventLogger> PrepareEventLogger()
    {
        var eventLoggerMock = new Mock<IFileEventLogger>();

        foreach (var item in _initialOffsets)
        {
            eventLoggerMock.Setup(v =>
                    v.ReadLastMessage<TestEvent>(It.Is<FileLogSegment>(s => s.PartitionId == item.Key), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LogMessage<TestEvent>() { Key = Guid.NewGuid().ToString("D"), Offset = item.Value });
        }

        eventLoggerMock.Setup(v => v.Write<TestEvent>(It.IsAny<LogMessage<TestEvent>>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()))
            .Callback<LogMessage<TestEvent>, FileLogSegment, CancellationToken>((logMessage, segment, _) => SaveLoggedEvent(logMessage, segment));

        eventLoggerMock.Setup(v => v.Poll<TestEvent>(It.IsAny<PollRequest>(), It.IsAny<FileLogSegment>(), It.IsAny<long>(),It.IsAny<CancellationToken>()))
            .Returns((PollRequest request, FileLogSegment segment, CancellationToken _) => PreparePollResults(request, segment));

        eventLoggerMock.Setup(v => v.Commit(It.IsAny<LogOffsetRequest>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<LogOffsetRequest, FileLogSegment, CancellationToken>((request, segment, _) => HandleCommitedEvent(request, segment));

        return eventLoggerMock;
    }


    private IAsyncEnumerable<LogMessage<TestEvent>> PreparePollResults(PollRequest request, FileLogSegment segment)
    {
        if (_loggedEvents.IsEmpty
            || !_unhandledEvents.TryGetValue(request.GroupName, out var unhandledEvents) 
            || unhandledEvents.IsEmpty)
        {
            return Array.Empty<LogMessage<TestEvent>>().ToAsyncEnumerable();
        }

        var loggedEvents = unhandledEvents.GroupBy(v => v.PartitionId)
            .ToDictionary(v => v.Key, v => v.ToArray());

        if (!loggedEvents.TryGetValue(segment.PartitionId, out var foundEvents))
        {
            return Array.Empty<LogMessage<TestEvent>>().ToAsyncEnumerable();
        }

        var resultEvents = foundEvents.Select(v => new LogMessage<TestEvent>
        {
            Payload = v.Payload,
            Key = v.Key!,
            Offset = v.Offset,
            Timestamp = v.Timestamp.ToUnixTimeMilliseconds(),
            Metadata = v.Metadata
        }).ToAsyncEnumerable();

        return resultEvents;
    }
    
    private void SaveLoggedEvent(LogMessage<TestEvent> logMessage, FileLogSegment segment)
    {
        var response = new LogResponse<TestEvent>
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

    private void HandledEventResponse(LogResponse<TestEvent> responseMessage)
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

    private void HandleCommitedEvent(LogOffsetRequest request, FileLogSegment segment)
    {
        if (!_unhandledEvents.TryGetValue(request.Key.ConsumerGroupName, out var unhandledEvents)
            || unhandledEvents.IsEmpty)
        {
            return;
        }

        _ = unhandledEvents.TryDequeue(out var response);
    }
    
    
    private IReadOnlyCollection<LogResponse<TestEvent>> GetLoggedEvents(string key)
    {
        return _loggedEvents.Where(v => v.Key == key).ToArray();
    }

    private void EnsureConsumerAssigment(EventConsumer<TestEvent> consumer, byte index, string groupName, int segmentsCount)
    {
        consumer.GetIndex().Should().Be(index);
        consumer.GetGroupName().Should().Be(groupName);
        consumer.GetSegmentsCount().Should().Be(segmentsCount);
    }

    private async Task Start(CancellationToken ct)
    {
        await _broker.Start(ct);
        await _producer.Start(ct);

        var consumers = _consumers.Select(v => v.Start(ct)).ToArray();
        await Task.WhenAll(consumers);
    }

    private async Task Publish(CancellationToken ct, params LogRequest<TestEvent>[] requests)
    {
        if (!requests.Any())
        {
            return;
        }

        foreach (var request in requests)
        {
            await _producer.Publish(request, ct);
        }

        await Task.Delay(100, ct);
    }

    public sealed record TestEvent
    {
        public Guid Id { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public required string Message { get; set; }
    }
}