using Microsoft.Extensions.Logging;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;
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
    private const byte EventTopicPartitions = 5;
    private const byte OffsetPartitions = 50;
    private const string OffsetsTopicName = "__consumer_offsets";
    private readonly FileEventLogBroker<TestEvent> _broker;
    private readonly EventLogTopic _topic;
    private readonly EventLogTopic _offsetsTopic;
    private const byte EventLogDivider = 10;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    static FileLogBrokerFixture()
    {
        LogSegmentDirectory.SetEventLoggingRoot(BaseDirectoryRoot);
    }

    public FileLogBrokerFixture()
    {
        var brokerLoggerMock = new Mock<ILogger<FileEventLogBroker<TestEvent>>>();

        var eventTopic = new EventLogTopic(EventTopicName, EventTopicPartitions);
        var offsetsTopic = new EventLogTopic(OffsetsTopicName, OffsetPartitions);

        var brokerSettings = new EventBrokerSettings { TopicName = offsetsTopic.Name, Partitions = offsetsTopic.Partitions };

        var eventLoggerMock = new Mock<IFileEventLogger>();
        var broker = new FileEventLogBroker<TestEvent>(brokerLoggerMock.Object, brokerSettings, 
            eventLogger: eventLoggerMock.Object, offsetLogger:eventLoggerMock.Object, eventTopic);

        _topic = eventTopic;
        _offsetsTopic = offsetsTopic;
        
        _broker = broker;
    }

    public FileEventLogBroker<TestEvent> GetBroker() => _broker;
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
                IsActive = false, // can't determine in tests
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
        LogSegmentDirectory.GetOrCreate(OffsetsTopicName, OffsetPartitions);
        LogSegmentDirectory.GetOrCreate(EventTopicName, OffsetPartitions);

        // await _broker.Start(CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // await _broker.Stop(CancellationToken.None);

        LogSegmentDirectory.Cleanup(EventTopicName);
        LogSegmentDirectory.Cleanup(OffsetsTopicName);

        Directory.Delete(BaseDirectoryRoot, true);

        return Task.CompletedTask;
    }
}