using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Serializer;
using Odyssey.HRMS.EventLogLite.Tests.Tool;
using System.Buffers;
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
        var latestMsg = await _logger.ReadLast<TestEvent>(logSegment, cts.Token);
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
            // BatchSize = 100,
            // TopicName = nameof(PollRequest.TopicName),
            // GroupName = nameof(PollRequest.GroupName),
            // RequestId = Guid.NewGuid(),
            // OccuredAt = now,
            StartPosition = position1
        };
        
        var messages =  await _logger.Poll<TestEvent>(pollRequest, logSegment, cts.Token).ToArrayAsync(cts.Token);

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
            Value = value1,
            Metadata = new Dictionary<string, object>(),
            RequestId = Guid.CreateVersion7(now)
        };
        
        var request2 = new LogOffsetRequest
        {
            Key = key2,
            Value = value2,
            Metadata = new Dictionary<string, object>(),
            RequestId = Guid.CreateVersion7(now)
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

    // [Theory, AutoData]
    // public async Task TestReadOffset(LogOffsetKey key1, LogOffsetValue value1, LogOffsetKey key2, LogOffsetValue value2, int randomNumber)
    // {
    //     const byte partition = 128;
    //     var now = DateTimeOffset.UtcNow;
    //     var cts = new CancellationTokenSource();
    //     var linesCount = randomNumber % 34;
    //
    //     var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
    //     var logFilePath = logSegment.GetLogFilePath();
    //
    //     var request1 = new LogOffsetRequest
    //     {
    //         Key = key1,
    //         OccuredAt = now,
    //         Metadata = new Dictionary<string, object>(),
    //         RequestId = Guid.CreateVersion7(now),
    //         Value = value1
    //     };
    //     
    //     var request2 = new LogOffsetRequest
    //     {
    //         Key = key2,
    //         OccuredAt = now,
    //         Metadata = new Dictionary<string, object>(),
    //         RequestId = Guid.CreateVersion7(now),
    //         Value = value2
    //     };
    //     
    //     _ = await _fixture.WriteManyLines(linesCount + 1, logFilePath, cts.Token);
    //     _ = await _logger.Commit(request1, logSegment, cts.Token);
    //     _ = await _logger.Commit(request2, logSegment, cts.Token);
    //
    //     var offsetMessage = await _logger.ReadSavedOffset(key1, logSegment, cts.Token);
    //     
    //     using var scope = new AssertionScope();
    //     offsetMessage.Should().NotBeNull();
    //     offsetMessage.Key.Should().Be(key1);
    // }
    
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

    [Theory, AutoData]
    public async Task WriteIndexes(string key, TestEvent payload, int randomNumber)
    {
        // Arrange
        const byte partition = 127;
        var itemsCount = randomNumber + 124 % 42;
        long wantedItemOffset = itemsCount % 3 + itemsCount % 4;
        var wantedItemsCount = 22;
        
        var cts = new CancellationTokenSource();
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
        var logMessages = Enumerable.Range(0, itemsCount).Select(v => LogMessage<TestEvent>.CreateForJson(request, v)).ToArray();
        
        // Act
        foreach (var item in logMessages)
        {
            _= await _logger.Write(item, logSegment, cts.Token);
        }
        var linesCount = _fixture.GetLinesCount(logSegment.GetLogFilePath());
        var wantedItemsSize = _fixture.GetBytesCount(logMessages[0]) * wantedItemsCount;
        var nearestStartPositionResult = _logger.FindNearestPosition(wantedItemOffset, logSegment);
        var batchItems = new  List<LogResponse<TestEvent>>();
        var sizeLimit = wantedItemsSize;
        var pollRequest = new PollRequest
        {

            // GroupName =   nameof(PollRequest.GroupName),
            // RequestId = Guid.NewGuid(),
            // OccuredAt = DateTimeOffset.UtcNow,
            StartPosition = nearestStartPositionResult.Position
        };
        
        await foreach (var logMessage in _logger.Poll<TestEvent>(pollRequest, logSegment, cts.Token).ConfigureAwait(false))
        {
            sizeLimit -= logMessage.RecordLength;
            if (sizeLimit <= 0)
            {
                break;
            }

            // skip prev msg 
            if (logMessage.Offset < wantedItemOffset)
            {
                continue;
            }
            
            var logResponse = new LogResponse<TestEvent>
            {
                Key = logMessage.Key,
                Payload = logMessage.Payload,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logMessage.Timestamp),
                Offset = logMessage.Offset,
                PartitionId = logSegment.Partition,
                Metadata = logMessage.Metadata
            };

            batchItems.Add(logResponse);
        }


        // Assert
        using var scope = new AssertionScope();
        linesCount.Should().Be(itemsCount + 1);
        batchItems.Should().NotBeEmpty();
        batchItems.Should().ContainSingle(v=>v.Offset == wantedItemOffset);
        batchItems.Count.Should().BeLessOrEqualTo(wantedItemsCount);
        // startPosition.Value.Should().BeGreaterThan(wantedItemsSize);
        // position.Start.Should().Be(0);
        // position.Next.Should().Be(size + 1);
        // linesCount.Should().Be(2);
    }

    [Theory, AutoData]
    public void WriteMessagesUsingJsonRowSerializer(string key, TestEvent payload, int randomNumber)
    {
        // Arrange
        const byte partition = 131;
        var itemsCount = randomNumber + 124 % 42;
        // long wantedItemOffset = itemsCount % 3 + itemsCount % 4;
        // var wantedItemsCount = 22;
        
        // var cts = new CancellationTokenSource();
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
        var logMessages = Enumerable.Range(0, itemsCount).Select(v => LogMessage<TestEvent>.Create(request, v)).ToArray();
        var sharedBuffer = new ArrayBufferWriter<byte>();
        var path = logSegment.GetLogFilePath();
        var bufferSize = 0;
        foreach (var item in logMessages)
        {
            bufferSize += LogSerializer.SerializeAsJsonRow(sharedBuffer, item);
        }
        
        // Act
        var segmentLength = _fixture.SaveBuffer(path, sharedBuffer);
        _fixture.CutAndCloseSegment(path, segmentLength);
        
        using var scope = new AssertionScope();
        bufferSize.Should().Be(segmentLength);
    }
    
    [Theory, AutoData]
    public async Task WriteMessagesBatchUsingJsonRowSerializer(string key, TestEvent payload, int randomNumber)
    {
        // Arrange
        const byte partition = 131;
        var itemsCount = randomNumber + 124 % 42;
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };
        var logSegment = FileLogSegment.New(partition, _fixture.CreateSegmentRoot(partition));
        var metadataCounter = 331;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var logMessages = Enumerable.Range(0, itemsCount).Select(v => LogMessage<TestEvent>.Create(request, v, timestamp+v)).ToArray();
        Array.ForEach(logMessages, logMessage =>
        {
            logMessage.Metadata = new Dictionary<string, object> { { "counter", metadataCounter++.ToString(LogSerializer.JsonInt32Format) } };
        });
        
        var sharedBuffer = new ArrayBufferWriter<byte>();
        var path = logSegment.GetLogFilePath();
        var bufferSize = 0;

        var batchSize = 33;
        int totalMessages = logMessages.Length;
        var batchesCount = (logMessages.Length + batchSize -1)/batchSize;
        var batchSources = new  List<LogMessageBatch<string, TestEvent>>(batchesCount);
        
        for (var i = 0; i < totalMessages; i += batchSize)
        {
            var currentBatchSize = Math.Min(batchSize, totalMessages - i);
            var firstBatchMsg = logMessages[i];
            var batchBaseOffset = firstBatchMsg.Offset;
            var firstTimestamp = firstBatchMsg.Timestamp;
            
            var items = new LogMessageBatchItem<string, TestEvent>[currentBatchSize];
    
            for (var j = 0; j < currentBatchSize; j++)
            {
                var msg = logMessages[i + j];
                items[j] = new LogMessageBatchItem<string, TestEvent>
                {
                    RecordLength = 0, // TBS
                    Attributes = 0, // TBD
                    OffsetDelta = msg.Offset - batchBaseOffset,
                    TimestampDelta = msg.Timestamp - firstTimestamp,
                    KeyLength = 0, // TBS
                    Key = msg.Key,
                    PayloadLength = 0, // TBS
                    Payload = msg.Payload,
                    MetadataLength =  0, // TBS
                    Metadata = msg.Metadata
                };
            }

            batchSources.Add(new LogMessageBatch<string, TestEvent>
            {
                BaseOffset = batchBaseOffset,
                BatchLength = 0, // TBS
                Version = 0, // TBD
                Checksum = 0, // TBD
                Attributes = 0, // TBD
                LastOffsetDelta = items[^1].OffsetDelta,
                FirstTimestamp = firstTimestamp,
                MaxTimestamp = items.Max(v=>v.TimestampDelta) + firstTimestamp,
                ProducerId = 0, // TBD
                ProducerEpoch = 0, // TBD
                BatchOrder = 0, // TBD
                ItemsCount = items.Length,
                Items = items
            });
        }
        
        foreach (var item in batchSources)
        {
            bufferSize += LogSerializer.SerializeAsJsonRow(sharedBuffer, item);
        }
        
        // Act
        var segmentLength = _fixture.SaveBuffer(path, sharedBuffer);
        _fixture.CutAndCloseSegment(path, segmentLength);
        
        var batchHeaders = _fixture.ReadHeaders(path).ToArray();
        var batchResults = await _fixture.ReadFile<LogMessageBatch<string, TestEvent>>(path).ToArrayAsync();
        
        using var scope = new AssertionScope();
        bufferSize.Should().Be(segmentLength);
        
        batchSources.Count.Should().Be(batchResults.Length);
        batchSources.Count.Should().Be(batchHeaders.Length);

        for (var i = 0; i < batchSources.Count; i++)
        {
            var sourceBatch = batchSources[i];
            var destinationBatch = batchResults[i];
            var batchHeader = batchHeaders[i];

            sourceBatch.Should().NotBeNull();
            destinationBatch.Should().NotBeNull();
            batchHeader.Should().NotBeNull();
            
            sourceBatch.BaseOffset.Should().Be(destinationBatch.BaseOffset);
            batchHeader.BaseOffset.Should().Be(destinationBatch.BaseOffset);
            
            sourceBatch.BatchLength.Should().Be(0);
            destinationBatch.BatchLength.Should().BeGreaterThan(0);
            batchHeader.BatchLength.Should().Be(destinationBatch.BatchLength);
            
            sourceBatch.LastOffsetDelta.Should().Be(destinationBatch.LastOffsetDelta);
            batchHeader.LastOffsetDelta.Should().Be(destinationBatch.LastOffsetDelta);
            
            sourceBatch.FirstTimestamp.Should().Be(destinationBatch.FirstTimestamp);
            batchHeader.FirstTimestamp.Should().Be(destinationBatch.FirstTimestamp);
            
            sourceBatch.MaxTimestamp.Should().Be(destinationBatch.MaxTimestamp);
            batchHeader.MaxTimestamp.Should().Be(destinationBatch.MaxTimestamp);
            
            sourceBatch.ItemsCount.Should().Be(destinationBatch.ItemsCount);
            batchHeader.ItemsCount.Should().Be(destinationBatch.ItemsCount);
            
            for(var j=0; j< batchSources[i].ItemsCount; j++)
            {
                var sourceItem = batchSources[i].Items[j];
                var destinationItem = batchResults[i].Items[j];
                
                sourceItem.Should().NotBeNull();
                destinationItem.Should().NotBeNull();

                sourceItem.RecordLength.Should().Be(0);
                destinationItem.RecordLength.Should().BeGreaterThan(0);
                
                sourceItem.OffsetDelta.Should().Be(destinationItem.OffsetDelta);
                sourceItem.TimestampDelta.Should().Be(destinationItem.TimestampDelta);
                
                sourceItem.KeyLength.Should().Be(0);
                destinationItem.KeyLength.Should().BeGreaterThan(0);

                sourceItem.Key.Should().Be(destinationItem.Key);
                
                sourceItem.PayloadLength.Should().Be(0);
                destinationItem.PayloadLength.Should().BeGreaterThan(0);
                
                sourceItem.Payload.Id.Should().Be(destinationItem.Payload.Id);
                sourceItem.Payload.OccurredAt.Should().Be(destinationItem.Payload.OccurredAt);
                sourceItem.Payload.Message.Should().Be(destinationItem.Payload.Message);
                sourceItem.Payload.Skipped.Should().Be(destinationItem.Payload.Skipped);
                
                sourceItem.MetadataLength.Should().Be(0);
                destinationItem.MetadataLength.Should().BeGreaterThan(0);
                
                sourceItem.Metadata?.Keys.Should().BeEquivalentTo(destinationItem.Metadata?.Keys);
            }
        }
        
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