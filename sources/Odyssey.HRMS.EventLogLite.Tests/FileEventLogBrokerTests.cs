using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Tests.Tool;
using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
public sealed class FileEventLogBrokerTests : IAsyncLifetime, IClassFixture<FileLogBrokerFixture>
{
    private readonly FileLogBrokerFixture _fixture;

    // ReSharper disable once ConvertToPrimaryConstructor
    public FileEventLogBrokerTests(FileLogBrokerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWorkingDirectoryWithoutSegments()
    {
        var topic = new EventLogTopic("box-box", 6);
        var workingDirectory = LogSegmentDirectory.GetOrCreate(topic);

        _fixture.CreateDirectories(workingDirectory, "1", "3", "5");

        var logSegments = LogSegmentDirectory.ScanOffsets(topic.Name).Values.SelectMany(v => v).ToArray();
        var subDirectories = _fixture.GetSubDirectories(workingDirectory);

        LogSegmentDirectory.Cleanup(topic.Name);

        using var scope = new AssertionScope();
        logSegments.Length.Should().Be(6);
        subDirectories.Count.Should().Be(6);
        subDirectories.SingleOrDefault(v => v.Name.Equals("0")).Should().NotBeNull();
        subDirectories.SingleOrDefault(v => v.Name.Equals("1")).Should().NotBeNull();
        subDirectories.SingleOrDefault(v => v.Name.Equals("2")).Should().NotBeNull();
        subDirectories.SingleOrDefault(v => v.Name.Equals("3")).Should().NotBeNull();
        subDirectories.SingleOrDefault(v => v.Name.Equals("4")).Should().NotBeNull();
        subDirectories.SingleOrDefault(v => v.Name.Equals("5")).Should().NotBeNull();
    }

    [Theory, AutoData]
    public async Task TestWorkingDirectoryWithSingleSegment(string topicName, string key1, TestEvent payload1, string key2, TestEvent payload2)
    {
        const byte partition = 1;
        using var cts = new CancellationTokenSource();
        var now = DateTimeOffset.UtcNow;
        var topic = new EventLogTopic(topicName, 3);
        var workingDirectory = LogSegmentDirectory.GetOrCreate(topic);
        var logSegment = FileLogSegment.New(partition, workingDirectory);
        long time1 = now.AddMinutes(-2.0).ToUnixTimeMilliseconds(), time2 = now.AddMinutes(1.0).ToUnixTimeMilliseconds();
        var logMessage1 = LogMessage<TestEvent>.Create(key1, payload1, 0) with { Timestamp = time1 };
        var logMessage2 = LogMessage<TestEvent>.Create(key2, payload2, 1) with { Timestamp = time2 };

        var logSegmentResult = await _fixture.Write(logSegment, [logMessage1, logMessage2], cts.Token);
        var logSegments = LogSegmentDirectory.ScanOffsets(topic.Name).Values.SelectMany(v => v).ToArray();
        var usedLogSegment = logSegments.Single(v => !v.IsEmpty());
        LogSegmentDirectory.Cleanup(topic.Name);

        using var scope = new AssertionScope();
        logSegmentResult.Should().NotBeNull();
        logSegmentResult.BaseOffset.Should().Be(logMessage1.Offset);
        logSegmentResult.BaseTime.Should().Be(logMessage1.Timestamp);

        logSegments.Length.Should().Be(3);
        usedLogSegment.Should().NotBeNull();
        usedLogSegment.BaseOffset.Should().Be(logMessage1.Offset);
        usedLogSegment.BaseTime.Should().Be(logMessage1.Timestamp);
        usedLogSegment.IsActive.Should().BeTrue();
    }

    [Theory, AutoData]
    public async Task TestWorkingDirectoryWithMultipleSegments(string topicName, string key1, TestEvent payload1, string key2, TestEvent payload2)
    {
        const byte partition = 1;
        const long baseOffset1 = 0L;
        const long baseOffset2 = 10000234510L;

        using var cts = new CancellationTokenSource();
        var now = DateTimeOffset.UtcNow;
        var topic = new EventLogTopic(topicName, 3);
        var workingDirectory = LogSegmentDirectory.GetOrCreate(topic);

        var time1 = now.AddMinutes(-8.0).ToUnixTimeMilliseconds();
        var time2 = now.AddMinutes(-2.0).ToUnixTimeMilliseconds();
        var time3 = now.AddMinutes(1.0).ToUnixTimeMilliseconds();

        var logSegment1 = FileLogSegment.New(partition, workingDirectory) with { BaseOffset = baseOffset1, BaseTime = time1 };
        var logSegment2 = FileLogSegment.New(partition, workingDirectory) with { BaseOffset = baseOffset2, BaseTime = time2 };

        var logMessage1 = LogMessage<TestEvent>.Create(key1, payload1, baseOffset1) with { Timestamp = time1 };
        var logMessage2 = LogMessage<TestEvent>.Create(key1, payload1, baseOffset2) with { Timestamp = time2 };
        var logMessage3 = LogMessage<TestEvent>.Create(key2, payload2, baseOffset2 + 1) with { Timestamp = time3 };

        var logSegmentResult1 = await _fixture.Write(logSegment1, [logMessage1], cts.Token);
        var logSegmentResult2 = await _fixture.Write(logSegment2, [logMessage2, logMessage3], cts.Token);
        var logSegmentScanResult = LogSegmentDirectory.ScanOffsets(topic.Name).Values.SelectMany(v => v).ToArray();

        var selectedPartitionLogSegments = logSegmentScanResult.Where(v => v.Partition == partition).ToArray();
        var activeSegment = selectedPartitionLogSegments.Length == 2 ? selectedPartitionLogSegments[0] : throw new InvalidOperationException();
        var notActiveSegment = selectedPartitionLogSegments.Length == 2 ? selectedPartitionLogSegments[1] : throw new InvalidOperationException();

        LogSegmentDirectory.Cleanup(topic.Name);

        using var scope = new AssertionScope();

        logSegmentResult1.Should().NotBeNull();
        logSegmentResult1.BaseOffset.Should().Be(logMessage1.Offset);
        logSegmentResult1.BaseTime.Should().Be(logMessage1.Timestamp);

        logSegmentResult2.Should().NotBeNull();
        logSegmentResult2.BaseOffset.Should().Be(logMessage2.Offset);
        logSegmentResult2.BaseTime.Should().Be(logMessage2.Timestamp);

        activeSegment.Should().NotBeNull();
        activeSegment.BaseOffset.Should().Be(logMessage2.Offset);
        activeSegment.BaseTime.Should().Be(logMessage2.Timestamp);
        activeSegment.IsActive.Should().BeTrue();

        notActiveSegment.Should().NotBeNull();
        notActiveSegment.BaseOffset.Should().Be(logMessage1.Offset);
        notActiveSegment.BaseTime.Should().Be(logMessage1.Timestamp);
        notActiveSegment.IsActive.Should().BeFalse();
    }

    [Theory, AutoData]
    public async Task TestBrokerStart(string topic1Name, string key1, TestEvent payload1, string key2, TestEvent payload2)
    {
        const byte partition = 1;
        const long baseOffset1 = 0L;
        const long baseOffset2 = 10000234510L;
        const int heartBeatInterval = 30_000;

        using var cts = new CancellationTokenSource();
        var topic1 = new EventLogTopic(topic1Name, Partitions: 3);

        var now = DateTimeOffset.UtcNow;
        long time1 = now.AddMinutes(-8.0).ToUnixTimeMilliseconds(),
            time2 = now.AddMinutes(-2.0).ToUnixTimeMilliseconds(),
            time3 = now.AddMinutes(1.0).ToUnixTimeMilliseconds();

        var workingDirectory1 = LogSegmentDirectory.GetOrCreate(topic1);
        var logSegment1 = FileLogSegment.New(partition, workingDirectory1) with { BaseOffset = baseOffset1, BaseTime = time1 };
        var logSegment2 = FileLogSegment.New(partition, workingDirectory1) with { BaseOffset = baseOffset2, BaseTime = time2, IsActive = true };

        var logMessage1 = LogMessage<TestEvent>.Create(key1, payload1, baseOffset1) with { Timestamp = time1 };
        var logMessage2 = LogMessage<TestEvent>.Create(key1, payload1, baseOffset2) with { Timestamp = time2 };
        var logMessage3 = LogMessage<TestEvent>.Create(key2, payload2, baseOffset2 + 1) with { Timestamp = time3 };

        _ = await _fixture.Write(logSegment1, [logMessage1], cts.Token);
        _ = await _fixture.Write(logSegment2, [logMessage2, logMessage3], cts.Token);

        var broker = _fixture.GetBroker();
        broker.Join(new ProducerBrokerConfig(topic1.Name, topic1.Partitions));

        // broker.Join(new ConsumerBrokerConfig(topic1.Name, Guid.NewGuid().ToString("D"), Replicas: 2));
        _ = broker.JoinGroup(new JoinGroupRequest(heartBeatInterval, (ConsumerGroupId)"test", (TopicName)topic1.Name, ConsumerMemberId.NotSet, 0));

        var exception = await Record.ExceptionAsync(async () => await broker.Start(cts.Token));
        exception.Should().BeNull();
    }

    [Theory, AutoData]
    public async Task TestLogFirstEventWithoutConsumer(string key, TestEvent payload)
    {
        const byte partition = 1;

        using var cts = new CancellationTokenSource();
        var broker = _fixture.GetBroker();
        var topic = _fixture.GetTopic();

        var request = new LogRequest<TestEvent> { Key = key, Payload = payload, PartitionId = partition, TopicName = topic.Name };
        broker.Join(new ProducerBrokerConfig(topic.Name, topic.Partitions));

        await broker.Start(cts.Token);

        var logResult = await broker.LogEvent(request, cts.Token);

        using var scope = new AssertionScope();
        logResult.Offset.Should().Be(0);
        logResult.TopicName.Should().Be(topic.Name);
        logResult.PartitionId.Should().Be(partition);
    }

    [Theory, AutoData]
    public async Task TestLogFirstEventWithConsumers(string key, TestEvent payload)
    {
        const byte partition = 1;
        const int heartBeatInterval = 30_000;

        using var cts = new CancellationTokenSource();
        var broker = _fixture.GetBroker();
        var topic = _fixture.GetTopic();
        var consumerGroupId = new ConsumerGroupId("test");
        var topicName = new TopicName(topic.Name);
        var consumer1MemberId = ConsumerMemberId.CreateNew();

        var logRequest = new LogRequest<TestEvent> { Key = key, Payload = payload, PartitionId = partition, TopicName = topic.Name };
        var pollRequest = new BatchPoolRequest
        {
            TopicName = topicName.Value,
            PartitionId = partition,
            GroupName = consumerGroupId.Value,
            ConsumerId = consumer1MemberId.Value,
            ConsumerGenerationId = 0,
            Offset = 0,
            MaxBytes = 50000,
            MaxWaitTimeMs = 500,
            RequestId = Guid.NewGuid(),
            OccuredAt = DateTimeOffset.UtcNow
        };

        broker.Join(new ProducerBrokerConfig(topic.Name, topic.Partitions));
        _ = broker.JoinGroup(new JoinGroupRequest(heartBeatInterval, consumerGroupId, topicName, consumer1MemberId, 0));
        _ = broker.JoinGroup(new JoinGroupRequest(heartBeatInterval, consumerGroupId, topicName, ConsumerMemberId.CreateNew(), 1));

        await broker.Start(cts.Token);

        var logResult = await broker.LogEvent(logRequest, cts.Token);
        var batchResult = await broker.PollEventsBatch<TestEvent>(pollRequest, cts.Token);

        using var scope = new AssertionScope();
        logResult.Offset.Should().Be(0);
        logResult.TopicName.Should().Be(topic.Name);
        logResult.PartitionId.Should().Be(partition);
        batchResult.Items.Should().ContainSingle(v => v.Key == key);
    }

    [Theory, AutoData]
    public async Task CommitOffset_WithSuccess(ConsumerGroupId groupId, ConsumerMemberId memberId, DateTimeOffset baseTime, string topic2Name)
    {
        const int consumerGeneration = 1;

        using var cts = new CancellationTokenSource();

        var broker = _fixture.GetBroker();
        var topic = _fixture.GetTopic();
        var commit1Time = baseTime.AddMilliseconds(-1234).ToUnixTimeMilliseconds();
        var commit2Time = baseTime.AddMilliseconds(-1343).ToUnixTimeMilliseconds();
        var commit3Time = baseTime.AddMilliseconds(-1313).ToUnixTimeMilliseconds();
        var topic1Name = topic.Name;
        
        var offsets = new[]
        {
            new CommitOffsetItem(topic1Name, 0, 100, commit1Time), 
            new CommitOffsetItem(topic1Name, 1, 102,commit2Time), 
            new CommitOffsetItem(topic2Name, 2, 93,commit3Time)
        };
        var commitRequest = new CommitOffsetRequest(groupId, memberId, consumerGeneration, offsets);

        var result = await broker.CommitOffset(commitRequest, cts.Token);

        using var scope = new AssertionScope();
        result.Error.Should().BeNull();
    }


    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}