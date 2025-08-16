using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;
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
    public async Task TestWriteOffset(string key, TestEvent payload)
    {
        const byte partition = 124;
        var cts = new CancellationTokenSource();
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
        var logMessage = LogMessage<TestEvent>.Create(request, 1);
        var logSegment = new FileLogSegment(partition, _fixture.GetFilePath(partition));

        _ = await _logger.Write(logMessage, logSegment, cts.Token);
        var linesCount = _fixture.GetLinesCount(logSegment.FilePath);
        
        linesCount.Should().Be(2);
    }

    [Theory, AutoData]
    public async Task TestLastMessage(string key, TestEvent payload, int randomNumber)
    {
        const byte partition = 125;
        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var liensCount = randomNumber % 23;
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
        var logMessage = LogMessage<TestEvent>.Create(request, liensCount + 1);
        var logSegment = new FileLogSegment(partition, _fixture.GetFilePath(partition));

        var position1 = await _fixture.WriteManyLines(liensCount, logSegment.FilePath, cts.Token);
        var position2 = await _logger.Write(logMessage, logSegment, cts.Token);

        var latestMsg = await _logger.ReadLastMessage<TestEvent>(logSegment, cts.Token);
        var linesCount = _fixture.GetLinesCount(logSegment.FilePath);

        using var scope = new AssertionScope();
        
        latestMsg.Should().NotBeNull();
        latestMsg!.Payload.Should().Be(payload);
        latestMsg.Key.Should().Be(key);
        latestMsg.Metadata.Should().NotBeNull();
        latestMsg.Timestamp.Should().BeCloseTo(now.ToUnixTimeMilliseconds(), (ulong)TimeSpan.FromSeconds(1).TotalMilliseconds);  
        linesCount.Should().Be(latestMsg.Offset + 1);

        position1.Should().BeLessThan(position2.Next);
    }

    [Theory, AutoData]
    public async Task TestPoll(string key1, TestEvent payload1, string key2, TestEvent payload2, int randomNumber)
    {
        const byte partition = 126;
        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var linesCount = randomNumber % 123;
        var newOffset = linesCount;
        var logSegment = new FileLogSegment(partition, _fixture.GetFilePath(partition));

        var logMessage1 = LogMessage<TestEvent>.Create(key1, payload1, ++newOffset);
        var logMessage2 = LogMessage<TestEvent>.Create(key2, payload2, ++newOffset);

        var position1 = await _fixture.WriteManyLines(linesCount, logSegment.FilePath, cts.Token);
        var position2Pair = await _logger.Write(logMessage1, logSegment, cts.Token);
        var position3Pair = await _logger.Write(logMessage2, logSegment, cts.Token);

        var pollRequest = new PollRequest
        {
            BatchSize = 100,
            TopicName = nameof(PollRequest.TopicName),
            GroupName = nameof(PollRequest.GroupName),
            RequestId = Guid.NewGuid(),
            OccuredAt = now
        };
        
        var messages =  await _logger.Poll<TestEvent>(pollRequest, logSegment, position1, cts.Token).ToArrayAsync(cts.Token);

        using var scope = new AssertionScope();
        
        position1.Should().Be(position2Pair.Start);
        position3Pair.Start.Should().Be(position2Pair.Next);
        messages.Length.Should().Be(2);
        messages.Should().Contain(v=>v.Key == key1);
        messages.Should().Contain(v => v.Key == key2);
    }

    [Theory, AutoData]
    public async Task TestCommit(LogOffsetKey key, LogOffsetValue value, int randomNumber)
    {
        const byte partition = 126;
        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var linesCount = randomNumber % 93;

        var logSegment = new FileLogSegment(partition, _fixture.GetFilePath(partition));

        var request = new LogOffsetRequest
        {
            Key = key,
            OccuredAt = now,
            Metadata = new Dictionary<string, object>(),
            RequestId = Guid.CreateVersion7(now),
            Value = value
        };
        
        var position1 = await _fixture.WriteManyLines(linesCount, logSegment.FilePath, cts.Token);
        var position2 = await _logger.Commit(request, logSegment, cts.Token);

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