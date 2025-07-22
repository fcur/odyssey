using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
public sealed class OneTopicWithCoupleConsumersTests
{
    private const string TopicName = "test";
    private const byte Partitions = 5;
    private const uint FileSizeLimitBytes = 1024 * 1024;
    private const int ChannelCapacity = 1000;
    private const string Group1Name = "Group1";
    private const string Group2Name = "Group2";
    private const string EventName = "Event1";

    private readonly Mock<IFileEventLogger> _eventLoggerMock;
    
    private readonly FileEventLogBroker<TestEvent> _broker;
    private readonly EventProducer<TestEvent> _producer;
    private readonly List<EventConsumer<TestEvent>> _consumers = [];
    private readonly ConcurrentBag<LogRespone<TestEvent>> _loggedEvents = new();

    private readonly Dictionary<byte, uint> _initialOffsets = new()
    {
        { 0, 33 },
        { 1, 44 },
        { 2, 24 },
        { 3, 29 },
        { 4, 39 }
    };

    public OneTopicWithCoupleConsumersTests()
    {
        var producerSettings = new EventProducerSettings { FileSizeLimitBytes = FileSizeLimitBytes, TopicName = TopicName, Partitions = Partitions };
        var topic = new EventLogTopic(TopicName, Partitions);
        
        _eventLoggerMock = PrepareEventLogger();
        _broker = new FileEventLogBroker<TestEvent>(_eventLoggerMock.Object, topic);
        _producer = new EventProducer<TestEvent>(_broker, producerSettings);

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
        
        _eventLoggerMock.Verify(v=>v.Write(It.IsAny<LogMessage<TestEvent>>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()), Times.Once);
        loggedMessage.Should().NotBeNull();
        loggedMessage?.Key.Should().Be(key);
        loggedMessage?.Payload.Should().Be(payload);
        loggedMessage?.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        loggedMessage?.Offset.Should().Be(_initialOffsets[3] + 1);
        loggedMessage?.PartitionId.Should().Be(3);
    }

    [Theory, AutoData]
    public async Task Should_Consume(TestEvent payload1, Guid key1, TestEvent payload2, Guid key2)
    {
        var ct = CancellationToken.None;
        var request1 = new LogRequest<TestEvent> { Key = key1.ToString("D"), Payload = payload1 };
        var request2 = new LogRequest<TestEvent> { Key = key2.ToString("D"), Payload = payload2 };
        
        await Start(ct);
        await Publish(ct, request1, request2);
        
        using var scope = new AssertionScope();
        
        
        
        
    }
    

    private void PrepareTopicConsumers(string groupName, byte replicaCount)
    {
        var consumerSettings = new EventConsumerSettings(groupName)
        {
            TopicName = TopicName, Capacity = ChannelCapacity, ReplicaCount = replicaCount, EventName = EventName
        };

        for (byte i = 0; i < replicaCount; i++)
        {
            var consumerImplMock = new Mock<IEventConsumerImpl<TestEvent>>();
            consumerImplMock.Setup(v => v.Handle(It.IsAny<LogRespone<TestEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var consumer = new EventConsumer<TestEvent>(_broker, consumerImplMock.Object, consumerSettings, i);
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
        
        eventLoggerMock.Setup(v=>v.Write<TestEvent>(It.IsAny<LogMessage<TestEvent>>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()))
            .Callback<LogMessage<TestEvent>, FileLogSegment, CancellationToken > ((logMessage, segment, _) => SaveLoggedEvent(logMessage, segment));

        return eventLoggerMock;
    }

    private void SaveLoggedEvent(LogMessage<TestEvent> logMessage, FileLogSegment segment)
    {
        var response = new LogRespone<TestEvent>
        {
            Key = logMessage.Key,
            Payload = logMessage.Payload,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
            Offset = logMessage.Offset,
            PartitionId = segment.PartitionId,
        };
        _loggedEvents.Add(response);
    }

    private IReadOnlyCollection<LogRespone<TestEvent>> GetLoggedEvents(string key)
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

    private async Task Publish( CancellationToken ct, params LogRequest<TestEvent>[] requests)
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