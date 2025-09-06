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

    [Theory, AutoData]
    public void TestWorkingDirectoryWithoutSegments(string name1, string name2, string name3)
    {
        var topic = new EventLogTopic("box-box", 6);
        var workingDirectory = LogSegmentDirectory.Init(topic);

        _fixture.CreateDirectories(workingDirectory, name1, "1", "3", name2, "5", name3);

        var logSegments = LogSegmentDirectory.Scan(topic.Name);
        var allFolders = _fixture.GetFolders(workingDirectory);

        LogSegmentDirectory.Cleanup(topic.Name);

        using var scope = new AssertionScope();
        logSegments.Should().BeEmpty();
        allFolders.Count.Should().Be(9);
        allFolders.SingleOrDefault(v => v.EndsWith("0")).Should().NotBeNull();
        allFolders.SingleOrDefault(v => v.EndsWith("1")).Should().NotBeNull();
        allFolders.SingleOrDefault(v => v.EndsWith("2")).Should().NotBeNull();
        allFolders.SingleOrDefault(v => v.EndsWith("3")).Should().NotBeNull();
        allFolders.SingleOrDefault(v => v.EndsWith("4")).Should().NotBeNull();
    }

    [Theory, AutoData]
    public async Task TestWorkingDirectoryWithSegments(string topicName, string key1, TestEvent payload1, string key2, TestEvent payload2)
    {
        const byte partition = 1;
        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var topic = new EventLogTopic(topicName, 3);
        var workingDirectory = LogSegmentDirectory.Init(topic);
        var logSegment = FileLogSegment.New(partition, workingDirectory);
        var time1 = now.AddMinutes(-2.0).ToUnixTimeMilliseconds();
        var time2 = now.AddMinutes(1.0).ToUnixTimeMilliseconds();
        var logMessage1 = LogMessage<TestEvent>.Create(key1, payload1, 0) with { Timestamp = time1 };
        var logMessage2 = LogMessage<TestEvent>.Create(key2, payload2, 1) with { Timestamp = time2 };

        var logSegmentResult = await _fixture.Write(logSegment, [logMessage1, logMessage2], cts.Token);
        var logSegments = LogSegmentDirectory.Scan(topic.Name);
        var logSegmentScanResult = logSegments.SingleOrDefault();

        LogSegmentDirectory.Cleanup(topic.Name);

        using var scope = new AssertionScope();
        logSegmentResult.Should().NotBeNull();
        logSegmentResult.BaseOffset.Should().Be(logMessage1.Offset);
        logSegmentResult.BaseTime.Should().Be(logMessage1.Timestamp);
        logSegmentResult.IsActive.Should().BeTrue();

        logSegments.Should().ContainSingle();
        logSegmentScanResult.Should().NotBeNull();
        logSegmentScanResult!.BaseOffset.Should().Be(logMessage1.Offset);
        logSegmentScanResult.BaseTime.Should().Be(logMessage1.Timestamp);
        logSegmentScanResult.IsActive.Should().BeTrue();
    }

    [Theory, AutoData]
    public async Task TestWorkingDirectoryWithMultipleSegments(string topicName, string key1, TestEvent payload1, string key2, TestEvent payload2)
    {
        const byte partition = 1;
        const long baseOffset1 = 0L;
        const long baseOffset2 = 10000234510L;

        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var topic = new EventLogTopic(topicName, 3);
        var workingDirectory = LogSegmentDirectory.Init(topic);
        var logSegment1 = FileLogSegment.New(partition, workingDirectory) with { BaseOffset = baseOffset1 };
        var logSegment2 = FileLogSegment.New(partition, workingDirectory) with { BaseOffset = baseOffset2 };
        
        var time0 = now.AddMinutes(-8.0).ToUnixTimeMilliseconds();
        var time1 = now.AddMinutes(-2.0).ToUnixTimeMilliseconds();
        var time2 = now.AddMinutes(1.0).ToUnixTimeMilliseconds();
        var logMessage0 = LogMessage<TestEvent>.Create(key1, payload1, baseOffset1) with { Timestamp = time0 };
        var logMessage1 = LogMessage<TestEvent>.Create(key1, payload1, baseOffset2) with { Timestamp = time1 };
        var logMessage2 = LogMessage<TestEvent>.Create(key2, payload2, baseOffset2 + 1) with { Timestamp = time2 };

        _fixture.CreateEmptyLogSegments([logSegment1, logSegment2]);
        var logSegmentResult1 = await _fixture.Write(logSegment1, [logMessage0], cts.Token);
        var logSegmentResult2 = await _fixture.Write(logSegment2, [logMessage1, logMessage2], cts.Token);
        var logSegments = LogSegmentDirectory.Scan(topic.Name);

        LogSegmentDirectory.Cleanup(topic.Name);

        using var scope = new AssertionScope();
        logSegmentResult2.Should().NotBeNull();
        logSegmentResult2.BaseOffset.Should().Be(logMessage1.Offset);
        logSegmentResult2.BaseTime.Should().Be(logMessage1.Timestamp);
        logSegmentResult2.IsActive.Should().BeTrue();

        logSegments.Should().ContainSingle();
        logSegmentScanResult.Should().NotBeNull();
        logSegmentScanResult!.BaseOffset.Should().Be(logMessage1.Offset);
        logSegmentScanResult.BaseTime.Should().Be(logMessage1.Timestamp);
        logSegmentScanResult.IsActive.Should().BeTrue();
    }


    [Theory, AutoData]
    public async Task TestLogEvent(string key, TestEvent payload)
    {
        var cts = new CancellationTokenSource();
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };

        var broker = _fixture.GetBroker();
        await broker.Start(cts.Token);

        var logResult = await broker.LogEvent(request, cts.Token);
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