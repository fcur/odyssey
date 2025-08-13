using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;
using Odyssey.HRMS.EventLogLite.Tests.Logging;
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
    private const long MaxOffset = 64; // results count = max offset - 1
    private static readonly TimeSpan PullDuration = TimeSpan.FromSeconds(5);
    private static readonly LogOffsetKey G1P0Key = new(Group1Name, TopicName, 0);
    private static readonly LogOffsetKey G1P1Key = new(Group1Name, TopicName, 1);
    private static readonly LogOffsetKey G1P2Key = new(Group1Name, TopicName, 2);
    private static readonly LogOffsetKey G1P3Key = new(Group1Name, TopicName, 3);
    private static readonly LogOffsetKey G1P4Key = new(Group1Name, TopicName, 4);
    private static readonly LogOffsetKey G2P0Key = new(Group2Name, TopicName, 0);
    private static readonly LogOffsetKey G2P1Key = new(Group2Name, TopicName, 1);
    private static readonly LogOffsetKey G2P2Key = new(Group2Name, TopicName, 2);
    private static readonly LogOffsetKey G2P3Key = new(Group2Name, TopicName, 3);
    private static readonly LogOffsetKey G2P4Key = new(Group2Name, TopicName, 4);

    private readonly ILoggerFactory _loggerFactory;
    private readonly TestHarnessSettings  _harnessSettings;
    
    public OneTopicWithCoupleConsumersTests(ITestOutputHelper outputHelper)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(outputHelper)).SetMinimumLevel(LogLevel.Trace);
        });

        var topic = new EventLogTopic(TopicName, Partitions);
        var brokerSettings = new EventBrokerSettings { TopicName = "__consumer_offsets", Partitions = 5 };
        var producerSettings = new EventProducerSettings { FileSizeLimitBytes = FileSizeLimitBytes, TopicName = TopicName, Partitions = Partitions };
        var consumerGroup1Settings = new EventConsumerSettings(Group1Name)
        {
            TopicName = TopicName,
            EventName = EventName,
            ReplicaCount = 5,
            BatchSize = BatchSize,
            PullDuration = PullDuration
        };
        var consumerGroup2Settings = new EventConsumerSettings(Group2Name)
        {
            TopicName = TopicName,
            EventName = EventName,
            ReplicaCount = 1,
            BatchSize = BatchSize,
            PullDuration = PullDuration
        };

        var latestOffsets  = new Dictionary<byte, long>
        {
            { 0, 33 },
            { 1, 44 },
            { 2, 24 },
            { 3, 29 },
            { 4, 39 }
        };
        
        var savedOffsets = new Dictionary<LogOffsetKey, long>
        {
            [G1P0Key] = 1,
            [G1P1Key] = 1,
            [G1P2Key] = 1,
            [G1P3Key] = 1,
            [G1P4Key] = 1,
            [G2P0Key] = 1,
            [G2P1Key] = 1,
            [G2P2Key] = 1,
            [G2P3Key] = 1,
            [G2P4Key] = 1,
        };
        
        _harnessSettings = new TestHarnessSettings
        {
            Topic = topic,
            BrokerSettings = brokerSettings,
            ProducerSettings = producerSettings,
            ConsumerSettings = [consumerGroup1Settings, consumerGroup2Settings],
            LatestOffsets = latestOffsets,
            SavedOffsets = savedOffsets,
            MaxOffset = MaxOffset
        };
    }
    
    [Fact]
    public async Task Should_Assign_Consumers()
    {
        var cts = new CancellationTokenSource();
        var testHarness = new TestHarness<TestEvent>(_harnessSettings, _loggerFactory);
        await testHarness.Start(cts.Token);
        await cts.CancelAsync();
        
        var assigmentStates = testHarness.GetAssigmentStates();

        using var scope = new AssertionScope();
        assigmentStates.Length.Should().Be(6);

        assigmentStates[0].Should().Be(new ConsumerAssigmentState(0, Group1Name, 1));
        assigmentStates[1].Should().Be(new ConsumerAssigmentState(1, Group1Name, 1));
        assigmentStates[2].Should().Be(new ConsumerAssigmentState(2, Group1Name, 1));
        assigmentStates[3].Should().Be(new ConsumerAssigmentState(3, Group1Name, 1));
        assigmentStates[4].Should().Be(new ConsumerAssigmentState(4, Group1Name, 1));
        assigmentStates[5].Should().Be(new ConsumerAssigmentState(0, Group2Name, 5));
    }

    [Theory, AutoData]
    public async Task Should_PublishTo_Partition3(TestEvent payload)
    {
        var key = "0342b673-b710-4a52-a60d-5993ae42d2ad";
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
       
        var cts = new CancellationTokenSource();
        var testHarness = new TestHarness<TestEvent>(_harnessSettings, _loggerFactory);
        
        await testHarness.Start(cts.Token);
        await testHarness.Publish([request], cts.Token);
        await cts.CancelAsync();
        
        var loggedMessagesCount = testHarness.LoggedEventsCount;
        var loggedMessage = testHarness.GetLoggerEvent(v => v.Key == key);
        
        using var scope = new AssertionScope();
        loggedMessagesCount.Should().Be(1);
        loggedMessage.Should().NotBeNull();
        loggedMessage!.Key.Should().Be(key);
        loggedMessage.Payload.Should().Be(payload);
        loggedMessage.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        loggedMessage.Offset.Should().Be(_harnessSettings.LatestOffsets[3] + 1);
        loggedMessage.PartitionId.Should().Be(3);
    }
    
    [Theory, AutoData]
    public async Task Should_StartConsume_FromBeginning(TestEvent payload1, Guid key1, TestEvent payload2, Guid key2)
    {
        var latestOffsets = _harnessSettings.LatestOffsets;
        var savedOffsets = new Dictionary<LogOffsetKey, long>
        {
            [G1P0Key] = 1, [G1P1Key] = 1, [G1P2Key] = 1, [G1P3Key] = 1, [G1P4Key] = 1, 
            [G2P0Key] = 1, [G2P1Key] = 1, [G2P2Key] = 1, [G2P3Key] = 1, [G2P4Key] = 1,
        };
        
        var cts = new CancellationTokenSource();
        var requests = new LogRequest<TestEvent>[]
        {
            new() { Key = key1.ToString("D"), Payload = payload1 }, 
            new() { Key = key2.ToString("D"), Payload = payload2 }
        };
        var expectedEventsCounter = savedOffsets.Values.Select(v => MaxOffset - v).Sum();
        var settings = _harnessSettings with { LatestOffsets = latestOffsets, SavedOffsets = savedOffsets };
        var testHarness = new TestHarness<TestEvent>(settings, _loggerFactory);
        
        await testHarness.Start(cts.Token);
        await testHarness.Publish(requests, cts.Token);
        await cts.CancelAsync();

        var loggerEvents = testHarness.GetLoggerEvents();
        var polledEvents = testHarness.PolledEventsCount;

        using var scope = new AssertionScope();

        loggerEvents.Should().Contain(v => v.Key == requests[0].Key);
        loggerEvents.Should().Contain(v => v.Key == requests[1].Key);
        polledEvents.Should().Be(expectedEventsCounter);
    }
    
    [Theory, AutoData]
    public async Task Should_StartConsume_FromKnownOffsets(TestEvent payload1, Guid key1, TestEvent payload2, Guid key2)
    {
        var latestOffsets = _harnessSettings.LatestOffsets;
        var random = new Random();
        var savedOffsets = new Dictionary<LogOffsetKey, long>
        {
            [G1P0Key] = NextRandomOffset(random, latestOffsets[0]), 
            [G1P1Key] = NextRandomOffset(random, latestOffsets[1]), 
            [G1P2Key] = NextRandomOffset(random, latestOffsets[2]), 
            [G1P3Key] = NextRandomOffset(random, latestOffsets[3]), 
            [G1P4Key] = NextRandomOffset(random, latestOffsets[4]), 
            [G2P0Key] = NextRandomOffset(random, latestOffsets[0]), 
            [G2P1Key] = NextRandomOffset(random, latestOffsets[1]),
            [G2P2Key] = NextRandomOffset(random, latestOffsets[2]),
            [G2P3Key] = NextRandomOffset(random, latestOffsets[3]), 
            [G2P4Key] = NextRandomOffset(random, latestOffsets[4])
        };
        
        var cts = new CancellationTokenSource();
        var requests = new LogRequest<TestEvent>[]
        {
            new() { Key = key1.ToString("D"), Payload = payload1 }, 
            new() { Key = key2.ToString("D"), Payload = payload2 }
        };
        var expectedEventsCounter = savedOffsets.Values.Select(v => MaxOffset - v).Sum();
        var settings = _harnessSettings with { LatestOffsets = latestOffsets, SavedOffsets = savedOffsets };
        var testHarness = new TestHarness<TestEvent>(settings, _loggerFactory);
        
        await testHarness.Start(cts.Token);
        await testHarness.Publish(requests, cts.Token);
        await cts.CancelAsync();
        
        var loggerEvents = testHarness.GetLoggerEvents();
        var polledEvents = testHarness.PolledEventsCount;

        using var scope = new AssertionScope();
        
        loggerEvents.Should().Contain(v => v.Key == requests[0].Key);
        loggerEvents.Should().Contain(v => v.Key == requests[1].Key);
        polledEvents.Should().Be(expectedEventsCounter);
        return;

        long NextRandomOffset(Random util, long maxOffset)
        {
            const long minOffset = 1;
            return util.NextInt64(minOffset, maxOffset);
        }
    }
}