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

        LogSegmentDirectory.Cleanup(topic.Name);

        using var scope = new AssertionScope();
        logSegmentResult.Should().NotBeNull();
        logSegmentResult.BaseOffset.Should().Be(logMessage1.Offset);
        logSegmentResult.BaseTime.Should().Be(logMessage1.Timestamp);
        
        logSegments.Should().ContainSingle();
        logSegments.Single().BaseOffset.Should().Be(logMessage1.Offset);
        logSegments.Single().BaseTime.Should().Be(logMessage1.Timestamp);
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