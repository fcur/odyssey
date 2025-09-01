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
    public void TestWorkingDirectory(string name1, string name2, string name3)
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