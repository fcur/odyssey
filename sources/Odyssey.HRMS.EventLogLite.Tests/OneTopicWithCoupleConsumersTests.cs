using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.EventLogLite.Tests;

public sealed class OneTopicWithCoupleConsumersTests
{
    private const string TopicName = "test";
    private const byte Partitions = 5;
    private const uint FileSizeLimitBytes = 1024 * 1024;
    private const int ChannelCapacity = 1000;
    private const string Group1Name = "Group1";
    private const string Group2Name = "Group2";
    private const string EventName = "Event1";

    private readonly FileEventLogBroker<TestEvent> _broker;
    private readonly EventProducer<TestEvent> _producer;
    private readonly List<EventConsumer<TestEvent>> _consumers = [];
    private readonly Dictionary<byte, uint> _initialOffsets = new() { { 0, 33 }, { 1, 44 }, { 2, 24 }, { 3, 29 }, { 4, 39 } };

    public OneTopicWithCoupleConsumersTests()
    {
        var producerSettings = new EventProducerSettings { FileSizeLimitBytes = FileSizeLimitBytes, TopicName = TopicName, Partitions = Partitions };
        var topic = new EventLogTopic(TopicName, Partitions);
        var eventLoggerMock = PrepareEventLogger();
        
        _broker = new FileEventLogBroker<TestEvent>(eventLoggerMock.Object, topic);
        _producer = new EventProducer<TestEvent>(_broker, producerSettings);

        PrepareTopicConsumers(Group1Name, 5);
        PrepareTopicConsumers(Group2Name, 1);
    }
    
    private void EnsureConsumer(EventConsumer<TestEvent>  consumer, byte index, string groupName, int segmentsCount)
    {
        consumer.GetIndex().Should().Be(index);
        consumer.GetGroupName().Should().Be(groupName);
        consumer.GetSegmentsCount().Should().Be(segmentsCount);
    }
    
    [Fact]
    public async Task Test_Consumers_Assignment()
    {
        await _broker.Start();

        using var scope = new AssertionScope();
        _consumers.Count.Should().Be(6);

        EnsureConsumer(_consumers[0], 0, Group1Name, 1);
        EnsureConsumer(_consumers[1], 1, Group1Name, 1);
        EnsureConsumer(_consumers[2], 2, Group1Name, 1);
        EnsureConsumer(_consumers[3], 3, Group1Name, 1);
        EnsureConsumer(_consumers[4], 4, Group1Name, 1);
        EnsureConsumer(_consumers[5], 0, Group2Name, 5);
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

            var consumer = new EventConsumer<TestEvent>(consumerImplMock.Object, consumerSettings, i);
            _broker.Join(consumer);
            _consumers.Add(consumer);
        }
    }

    private Mock<IFileEventLogger> PrepareEventLogger()
    {
        var eventLoggerMock = new Mock<IFileEventLogger>();
        
        foreach (var item in _initialOffsets)
        {
            eventLoggerMock.Setup(v => v.ReadLastMessage<TestEvent>(It.Is<FileLogSegment>(s => s.PartitionId == item.Key), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LogMessage<TestEvent>() { Key = Guid.NewGuid().ToString("D"), Offset = item.Value });
        }

        return eventLoggerMock;
    }

    public sealed record TestEvent
    {
        public Guid Id { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public required string Message { get; set; }
    }
}