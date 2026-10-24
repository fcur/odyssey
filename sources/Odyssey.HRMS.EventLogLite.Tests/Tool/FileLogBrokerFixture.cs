using Microsoft.Extensions.Logging;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Odyssey.HRMS.EventLogLite.Tests.Tool;

[ExcludeFromCodeCoverage]
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class FileLogBrokerFixture : IAsyncLifetime
{
    private const string BaseDirectoryRoot = "../../../../../FileEventLogBrokerTests";
    private const string EventTopicName = "test_event";
    private const byte EventTopicPartitions = 3;
    private const byte OffsetPartitions = 5;
    private const string OffsetsTopicName = "__consumer_offsets";
    private const byte EventLogDivider = 10;
    
    private readonly FileEventLogBroker _broker;
    private readonly EventLogTopic _topic;
    private readonly EventLogTopic _offsetsTopic;
    private readonly ConcurrentDictionary<FileLogSegment, ConcurrentQueue<LogMessage<TestEvent>>> _logMessages = new();
    private readonly ConcurrentDictionary<FileLogSegment,ConcurrentQueue<LogMessage<LogOffsetMessage>>> _offsets = new();
    private readonly ConcurrentDictionary<FileLogSegment, ConcurrentDictionary<long, long> > _offsetsMap = new();

    private long _logMessageNextPosition = 0;
    private long _logOffsetNextPosition = 0;
    
    
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    static FileLogBrokerFixture()
    {
        LogSegmentDirectory.SetEventLoggingRoot(BaseDirectoryRoot);
    }
    
    public FileLogBrokerFixture()
    {
        var brokerLoggerMock = new Mock<ILogger<FileEventLogBroker>>();

        var eventTopic = new EventLogTopic(EventTopicName, EventTopicPartitions);
        var offsetsTopic = new EventLogTopic(OffsetsTopicName, OffsetPartitions);

        var brokerSettings = new EventBrokerSettings { TopicName = offsetsTopic.Name, Partitions = offsetsTopic.Partitions };

        var eventLoggerMock = new Mock<IFileEventLogger>();
        var offsetLoggerMock = new Mock<IFileEventLogger>();

        eventLoggerMock.Setup(v => v.Write(It.IsAny<LogMessage<TestEvent>>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogMessage<TestEvent> msg, FileLogSegment segment, CancellationToken _) =>
            {
                var queue = _logMessages.GetOrAdd(segment, _ => new ConcurrentQueue<LogMessage<TestEvent>>());
                
                msg.KeyLength = JsonSerializer.SerializeToUtf8Bytes(msg.Key).Length;
                msg.PayloadLength = JsonSerializer.SerializeToUtf8Bytes(msg.Payload).Length;
                
                var objBytes = JsonSerializer.SerializeToUtf8Bytes(msg);
                var position = Interlocked.Add(ref _logMessageNextPosition, objBytes.Length);
                
                queue.Enqueue(msg);
                
                var index = _offsetsMap.GetOrAdd(segment, _ => new ConcurrentDictionary<long, long>());
                var startPosition = position - objBytes.Length;
                index[msg.Offset] = startPosition;

                return new PositionPair(startPosition, position + 1);
            });

        eventLoggerMock.Setup(v => v.Poll<TestEvent>(It.IsAny<PollRequest>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()))
            .Returns((PollRequest request, FileLogSegment segment, CancellationToken ct) =>
            {
                if (!_offsetsMap.TryGetValue(segment, out var map) || !_logMessages.TryGetValue(segment, out var queue))
                {
                    return Array.Empty<LogMessage<TestEvent>>().ToAsyncEnumerable();
                }

                return ConvertQueueToAsyncEnumerable(queue, v => map[v.Offset] >= request.StartPosition, ct);
            })
            .Callback<PollRequest, FileLogSegment, CancellationToken>((request, segment, _) => { });
        
        offsetLoggerMock.Setup(v => v.Write(It.IsAny<LogMessage<LogOffsetMessage>>(), It.IsAny<FileLogSegment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogMessage<LogOffsetMessage> msg, FileLogSegment segment, CancellationToken _) =>
            {
                var queue = _offsets.GetOrAdd(segment, _ => new ConcurrentQueue<LogMessage<LogOffsetMessage>>());
                queue.Enqueue(msg);
                var objBytes = JsonSerializer.SerializeToUtf8Bytes(msg);
                var position = Interlocked.Add(ref _logOffsetNextPosition, objBytes.Length);

                return new PositionPair(position - objBytes.Length, position + 1);
            });     

        var broker = new FileEventLogBroker(brokerLoggerMock.Object, brokerSettings, eventLoggerMock.Object, offsetLoggerMock.Object);

        _topic = eventTopic;
        _offsetsTopic = offsetsTopic;
        _broker = broker;
    }

    public FileEventLogBroker GetBroker() => _broker;
    public EventLogTopic GetTopic() => _topic;
    public EventLogTopic GetOffsetsTopic() => _offsetsTopic;

    public void CreateDirectories(string workingDirectory, params string[] folders)
    {
        foreach (var item in folders)
        {
            var newFolder = Path.Combine(workingDirectory, item);
            Directory.CreateDirectory(newFolder);
        }
    }

    // public string GetFileLogSegmentRoot(string workingDirectory, byte partition)
    // {
    //     var rootPath = Path.Combine(workingDirectory, partition.ToString());
    //     if (!Directory.Exists(rootPath))
    //     {
    //         Directory.CreateDirectory(rootPath);
    //     }
    //     
    //     return workingDirectory;
    // }

    public async Task CreateEmptyLogSegments(FileLogSegment[] segments, CancellationToken cancellationToken)
    {
        if (segments.Length == 0)
        {
            return;
        }

        const long newItemPosition = 0;

        foreach (var segment in segments)
        {
            var indexPath = segment.GetIndexFilePath();
            var timeIndexPath = segment.GetTimeIndexFilePath();
            var logPath = segment.GetLogFilePath();

            var offsetIndexesBytes = MemoryMarshal.AsBytes<long>(new[] { segment.BaseOffset, newItemPosition }).ToArray();
            await using var offsetIndexWriter = new BinaryWriter(File.Open(indexPath, FileMode.OpenOrCreate, FileAccess.Write));
            offsetIndexWriter.Write(offsetIndexesBytes);

            var timeIndexes = MemoryMarshal.AsBytes<long>(new[] { segment.BaseTime, newItemPosition }).ToArray();
            await using var timeIndexWriter = new BinaryWriter(File.Open(timeIndexPath, FileMode.OpenOrCreate, FileAccess.Write));
            timeIndexWriter.Write(timeIndexes);

            // await File.Create(indexPath).DisposeAsync();
            // await File.Create(timeIndexPath).DisposeAsync();
            await File.Create(logPath).DisposeAsync();
        }
    }

    public async Task<FileLogSegment> Write<TEvent>(FileLogSegment segment, LogMessage<TEvent>[] messages, CancellationToken cancellationToken)
        where TEvent : class
    {
        if (messages.Length == 0)
        {
            return segment;
        }

        await using var logSegmentWriter = new FileStream(segment.GetLogFilePath(), FileMode.OpenOrCreate, FileAccess.Write);
        await using var offsetIndexWriter = new BinaryWriter(File.Open(segment.GetIndexFilePath(), FileMode.OpenOrCreate, FileAccess.Write));
        await using var timeIndexWriter = new BinaryWriter(File.Open(segment.GetTimeIndexFilePath(), FileMode.OpenOrCreate, FileAccess.Write));

        logSegmentWriter.Seek(0, SeekOrigin.End);
        offsetIndexWriter.Seek(0, SeekOrigin.End);
        timeIndexWriter.Seek(0, SeekOrigin.End);

        foreach (var item in messages)
        {
            var offsetIndexesBytes = MemoryMarshal.AsBytes<long>(new[] { item.Offset, logSegmentWriter.Position }).ToArray();
            var timeIndexes = MemoryMarshal.AsBytes<long>(new[] { item.Timestamp, logSegmentWriter.Position }).ToArray();

            await JsonSerializer.SerializeAsync(logSegmentWriter, item, SerializerOptions, cancellationToken);
            await logSegmentWriter.WriteAsync(new[] { EventLogDivider }, cancellationToken);

            offsetIndexWriter.Write(offsetIndexesBytes);
            timeIndexWriter.Write(timeIndexes);

            segment = segment with
            {
                Size = logSegmentWriter.Length,
                BaseOffset = segment.IsEmpty() ? item.Offset : segment.BaseOffset,
                BaseTime = segment.IsEmpty() ? item.Timestamp : segment.BaseTime,
                IsActive = segment.IsActive // can't determine in tests
            };
        }

        return segment;
    }

    public IReadOnlyCollection<DirectoryInfo> GetSubDirectories(string workingDirectory)
    {
        return Directory.GetDirectories(workingDirectory).Select(v => new DirectoryInfo(v)).ToArray();
    }

    public Task InitializeAsync()
    {
        _ = LogSegmentDirectory.GetOrCreate(_topic.Name, _topic.Partitions);
        _ = LogSegmentDirectory.GetOrCreate(_offsetsTopic.Name, _offsetsTopic.Partitions);

        // await _broker.Start(CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // await _broker.Stop(CancellationToken.None);

        LogSegmentDirectory.Cleanup(_topic.Name);
        LogSegmentDirectory.Cleanup(_offsetsTopic.Name);

        Directory.Delete(BaseDirectoryRoot, true);

        return Task.CompletedTask;
    }

    private async IAsyncEnumerable<T> ConvertQueueToAsyncEnumerable<T>(ConcurrentQueue<T> queue, Predicate<T>? filter= null, CancellationToken ct = default)
    {
        while (queue.TryDequeue(out var item) )
        {
            ct.ThrowIfCancellationRequested();
            if (filter != null && filter(item))
            {
                yield return item;
            }
            
            await Task.Yield(); 
        }
    }
}