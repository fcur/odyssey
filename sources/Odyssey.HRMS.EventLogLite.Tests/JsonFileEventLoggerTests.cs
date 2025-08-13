using AutoFixture.Xunit2;
using FluentAssertions;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
public sealed class JsonFileEventLoggerTests : IAsyncLifetime, IClassFixture<FileSegmentFixture>
{
    private readonly JsonFileEventLogger _logger;
    private readonly FileSegmentFixture _fixture;

    // ReSharper disable once ConvertToPrimaryConstructor
    public JsonFileEventLoggerTests(FileSegmentFixture fixture)
    {
        _logger = new JsonFileEventLogger();
        _fixture = fixture;
    }

    [Theory, AutoData]
    public async Task TestWriteOffset(string key, TestEvent payload, byte partition)
    {
        var cts = new CancellationTokenSource();
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
        var logMessage = LogMessage<TestEvent>.Create(request, 1);
        var logSegment = new FileLogSegment(partition, _fixture.GetFilePath(partition));

        await _logger.Write(logMessage, logSegment, cts.Token);
        var linesCount = _fixture.GetLinesCount(logSegment.FilePath);
        
        linesCount.Should().Be(2, "the first line must be empty");
    }

    [Theory, AutoData]
    public async Task TestLastMessage(string key, TestEvent payload, byte partition, int randomNumber)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cts = new CancellationTokenSource();
        var liensCount = randomNumber % 123;
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
        var logMessage = LogMessage<TestEvent>.Create(request, liensCount + 1);
        var logSegment = new FileLogSegment(partition, _fixture.GetFilePath(partition));

        await _fixture.WriteManyLines(liensCount, logSegment.FilePath, cts.Token);
        await _logger.Write(logMessage, logSegment, cts.Token);

        var latestMsg = await _logger.ReadLastMessage<TestEvent>(logSegment, cts.Token);
        var linesCount = _fixture.GetLinesCount(logSegment.FilePath);

        latestMsg.Should().NotBeNull();
        latestMsg!.Payload.Should().Be(payload);
        latestMsg.Key.Should().Be(key);
        latestMsg.Metadata.Should().NotBeNull();
        latestMsg.Timestamp.Should().BeCloseTo(now, (ulong)TimeSpan.FromSeconds(1).TotalMilliseconds);  
        linesCount.Should().Be(latestMsg.Offset + 1);
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