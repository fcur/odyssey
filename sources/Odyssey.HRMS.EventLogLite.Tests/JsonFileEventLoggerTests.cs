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
    public async Task TestWriteManyLines(int randomNumber)
    {
        const byte partition = 121;
        var linesCount = randomNumber % 21;
        var cts = new CancellationTokenSource();
        
        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
        var logFilePath = logSegment.GetLogFilePath();
        
        var position = await _fixture.WriteManyLines(linesCount, logFilePath, cts.Token);
        var linesCountResult = _fixture.GetLinesCount(logFilePath);
        
        using var scope = new AssertionScope();
        position.Should().Be(linesCount - 1);
        linesCountResult.Should().Be(linesCount);
    }
    
    [Theory, AutoData]
    public async Task TestWriteOffset(string key, TestEvent payload)
    {
        const byte partition = 124;
        var cts = new CancellationTokenSource();
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
        var logMessage = LogMessage<TestEvent>.Create(request, 1);
        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));

        var position = await _logger.Write(logMessage, logSegment, cts.Token);
        var size = _fixture.GetBytesCount(logMessage);
        var linesCount = _fixture.GetLinesCount(logSegment.GetLogFilePath());

        position.Start.Should().Be(0);
        position.Next.Should().Be(size + 1);
        linesCount.Should().Be(2);
    }

    [Theory, AutoData]
    public async Task TestLastMessage(string key, TestEvent payload, int randomNumber)
    {
        const byte partition = 125;
        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var linesCount = randomNumber % 23;
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
        var logMessage = LogMessage<TestEvent>.Create(request, linesCount + 1);
        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
        var logFilePath = logSegment.GetLogFilePath();

        // additional line for new message
        var position1 = await _fixture.WriteManyLines(linesCount + 1, logFilePath, cts.Token);
        var position2 = await _logger.Write(logMessage, logSegment, cts.Token);
        var latestMsg = await _logger.ReadLastMessage<TestEvent>(logSegment, cts.Token);
        var size = _fixture.GetBytesCount(logMessage);
        var linesCountResult = _fixture.GetLinesCount(logFilePath);

        using var scope = new AssertionScope();
        
        latestMsg.Should().NotBeNull();
        latestMsg!.Payload.Should().Be(payload);
        latestMsg.Key.Should().Be(key);
        latestMsg.Metadata.Should().NotBeNull();
        latestMsg.Timestamp.Should().BeCloseTo(now.ToUnixTimeMilliseconds(), (ulong)TimeSpan.FromSeconds(1).TotalMilliseconds);  
        linesCountResult.Should().Be(latestMsg.Offset + 1);
        position1.Should().Be(position2.Start);
        position2.Next.Should().Be(position1 + size + 1);
    }

    [Theory, AutoData]
    public async Task TestPoll(string key1, TestEvent payload1, string key2, TestEvent payload2, int randomNumber)
    {
        const byte partition = 126;
        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var linesCount = randomNumber % 123;
        var newOffset = linesCount;
        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
        var logFilePath = logSegment.GetLogFilePath();

        var logMessage1 = LogMessage<TestEvent>.Create(key1, payload1, ++newOffset);
        var logMessage2 = LogMessage<TestEvent>.Create(key2, payload2, ++newOffset);

        var position1 = await _fixture.WriteManyLines(linesCount + 1, logFilePath, cts.Token);
        var position2 = await _logger.Write(logMessage1, logSegment, cts.Token);
        var linesCount1 = _fixture.GetLinesCount(logFilePath);
        var size1 = _fixture.GetBytesCount(logMessage1);
        var position3 = await _logger.Write(logMessage2, logSegment, cts.Token);
        var size2 = _fixture.GetBytesCount(logMessage2);
        var linesCount2 = _fixture.GetLinesCount(logFilePath);
        
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
        
        position1.Should().Be(position2.Start);
        position2.Next.Should().Be(position1 + size1 + 1);
        position3.Start.Should().Be(position2.Next);
        position3.Next.Should().Be(position1 + size1 + 1 + size2 + 1);
        linesCount1.Should().Be(linesCount + 2);
        linesCount2.Should().Be(linesCount + 3);
        
        messages.Length.Should().Be(2);
        messages.Should().Contain(v=>v.Key == key1);
        messages.Should().Contain(v => v.Key == key2);
    }

    [Theory, AutoData]
    public async Task TestCommit(LogOffsetKey key1, LogOffsetValue value1, LogOffsetKey key2, LogOffsetValue value2, int randomNumber)
    {
        const byte partition = 127;
        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var linesCount = randomNumber % 34;

        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
        var logFilePath = logSegment.GetLogFilePath();

        var request1 = new LogOffsetRequest
        {
            Key = key1,
            OccuredAt = now,
            Metadata = new Dictionary<string, object>(),
            RequestId = Guid.CreateVersion7(now),
            Value = value1
        };
        
        var request2 = new LogOffsetRequest
        {
            Key = key2,
            OccuredAt = now,
            Metadata = new Dictionary<string, object>(),
            RequestId = Guid.CreateVersion7(now),
            Value = value2
        };
        
        var position1 = await _fixture.WriteManyLines(linesCount + 1, logFilePath, cts.Token);
        var position2 = await _logger.Commit(request1, logSegment, cts.Token);
        var linesCount1 = _fixture.GetLinesCount(logFilePath);
        var position3 = await _logger.Commit(request2, logSegment, cts.Token);
        var linesCount2 = _fixture.GetLinesCount(logFilePath);

        using var scope = new AssertionScope();
        position1.Should().Be(position2.Start);
        position3.Start.Should().Be(position2.Next);
        
        linesCount1.Should().Be(linesCount + 2);
        linesCount2.Should().Be(linesCount + 3);
    }

    [Theory, AutoData]
    public async Task TestReadOffset(LogOffsetKey key1, LogOffsetValue value1, LogOffsetKey key2, LogOffsetValue value2, int randomNumber)
    {
        const byte partition = 128;
        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var linesCount = randomNumber % 34;

        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
        var logFilePath = logSegment.GetLogFilePath();

        var request1 = new LogOffsetRequest
        {
            Key = key1,
            OccuredAt = now,
            Metadata = new Dictionary<string, object>(),
            RequestId = Guid.CreateVersion7(now),
            Value = value1
        };
        
        var request2 = new LogOffsetRequest
        {
            Key = key2,
            OccuredAt = now,
            Metadata = new Dictionary<string, object>(),
            RequestId = Guid.CreateVersion7(now),
            Value = value2
        };
        
        _ = await _fixture.WriteManyLines(linesCount + 1, logFilePath, cts.Token);
        _ = await _logger.Commit(request1, logSegment, cts.Token);
        _ = await _logger.Commit(request2, logSegment, cts.Token);

        var offsetMessage = await _logger.ReadSavedOffset(key1, logSegment, cts.Token);
        
        using var scope = new AssertionScope();
        offsetMessage.Should().NotBeNull();
        offsetMessage.Key.Should().Be(key1);
    }
    
    [Theory, AutoData]
    public async Task TestWriteBatch(string key1, TestEvent payload1, string key2, TestEvent payload2, int randomNumber)
    {
        const byte partition = 129;
        var now = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var linesCount = randomNumber % 22;
        var newOffset = linesCount;
        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
        var logFilePath = logSegment.GetLogFilePath();

        var logMessage1 = LogMessage<TestEvent>.Create(key1, payload1, ++newOffset);
        var logMessage2 = LogMessage<TestEvent>.Create(key2, payload2, ++newOffset);

        var position1 = await _fixture.WriteManyLines(linesCount + 1, logFilePath, cts.Token);
        var linesCount1 = _fixture.GetLinesCount(logFilePath);
        var position2 = await _logger.WriteBatch([logMessage1, logMessage2], logSegment, cts.Token);
        var size1 = _fixture.GetBytesCount(logMessage1);
        var size2 = _fixture.GetBytesCount(logMessage2);
        var linesCount2 = _fixture.GetLinesCount(logFilePath);
        
        
        using var scope = new AssertionScope();
        
        position1.Should().Be(position2.Start);
        position2.Next.Should().Be(position1 + size1 + 1 + size2 + 1);
        linesCount1.Should().Be(linesCount + 1);
        linesCount2.Should().Be(linesCount + 3);
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