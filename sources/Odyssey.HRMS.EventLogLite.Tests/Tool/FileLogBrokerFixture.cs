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
    private const string TopicName = "test_event";
    private const byte Partitions = 5;
    private const string OffsetsTopic = "__consumer_offsets";
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
        var brokerSettings = new EventBrokerSettings { TopicName = OffsetsTopic, Partitions = Partitions };
        var topic = new EventLogTopic(TopicName, Partitions);
        var offsetsTopic = new EventLogTopic(OffsetsTopic, Partitions);
        var eventLoggerMock = new Mock<IFileEventLogger>();
        var broker = new FileEventLogBroker<TestEvent>(brokerLoggerMock.Object, brokerSettings, eventLoggerMock.Object, eventLoggerMock.Object, topic);

        _topic = topic;
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

    public void CreateEmptyLogSegments(FileLogSegment[] segments)
    {
        if (segments.Length == 0)
        {
            return;
        }

        foreach (var segment in segments)
        {
            var logPath = segment.GetLogFilePath();
            var indexPath = segment.GetIndexFilePath();
            var timeIndexPath = segment.GetTimeIndexFilePath();
            
            File.Create(logPath).Dispose();
            File.Create(indexPath).Dispose();
            File.Create(timeIndexPath).Dispose();
        }
    }
    
    public async Task<FileLogSegment> Write<TEvent>(FileLogSegment segment, LogMessage<TEvent>[] messages, CancellationToken cancellationToken) where TEvent : class
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
        return Directory.GetDirectories(workingDirectory).Select(v=> new DirectoryInfo(v)).ToArray();
    }
    
    public Task InitializeAsync()
    {
        LogSegmentDirectory.Init(OffsetsTopic, Partitions);
        LogSegmentDirectory.Init(TopicName, Partitions);
        
        // await _broker.Start(CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // await _broker.Stop(CancellationToken.None);

        LogSegmentDirectory.Cleanup(TopicName);
        LogSegmentDirectory.Cleanup(OffsetsTopic);
        
        Directory.Delete(BaseDirectoryRoot, true);

        return Task.CompletedTask;
    }
}