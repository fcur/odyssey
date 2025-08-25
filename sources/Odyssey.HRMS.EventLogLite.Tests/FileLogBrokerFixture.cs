using Microsoft.Extensions.Logging;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Producer;
using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class FileLogBrokerFixture : IAsyncLifetime
{
    private const string BaseDirectoryRoot = "../../../../../FileEventLogBrokerTests";
    private const string TopicName = "test_event";
    private const byte Partitions = 5;
    private const string OffsetsTopic = "__consumer_offsets";
    private readonly FileEventLogBroker<TestEvent> _broker;

    static FileLogBrokerFixture()
    {
        FileLogSegment.SetEventLoggingRoot(BaseDirectoryRoot);
    }
    
    public FileLogBrokerFixture()
    {
        var brokerLoggerMock = new Mock<ILogger<FileEventLogBroker<TestEvent>>>();
        var brokerSettings = new EventBrokerSettings { TopicName = OffsetsTopic, Partitions = Partitions };
        var topic = new EventLogTopic(TopicName, Partitions);
        var eventLoggerMock = new Mock<IFileEventLogger>();

        _broker = new FileEventLogBroker<TestEvent>(brokerLoggerMock.Object, brokerSettings, eventLoggerMock.Object, eventLoggerMock.Object, topic);
    }

    public FileEventLogBroker<TestEvent> GetBroker() => _broker;

    public void InitFolders(EventLogTopic topic, params string[] folders)
    {
        var baseDirectory = FileLogSegment.GetEventLoggingRoot();
        var workingDirectory = Path.GetFullPath(Path.Combine(baseDirectory, topic.Name));
        
        foreach (var item in folders)
        {
            var newFolder = Path.Combine(workingDirectory, item);
            Directory.CreateDirectory(newFolder);
        }
    }

    public IReadOnlyCollection<string> GetFolders(EventLogTopic topic)
    {
        var baseDirectory = FileLogSegment.GetEventLoggingRoot();
        var workingDirectory = Path.GetFullPath(Path.Combine(baseDirectory, topic.Name));

        return Directory.GetDirectories(workingDirectory);
    }
    
    public Task InitializeAsync()
    {
        InitWorkingDirectory(TopicName, Partitions);
        InitWorkingDirectory(OffsetsTopic, Partitions);

        // await _broker.Start(CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // await _broker.Stop(CancellationToken.None);

        CleanupWorkingDirectory(TopicName);
        CleanupWorkingDirectory(OffsetsTopic);

        return Task.CompletedTask;
    }

    public IReadOnlyCollection<string> InitWorkingDirectory(EventLogTopic topic)
    {
        return InitWorkingDirectory(topic.Name, topic.Partitions);
    }
    
    public IReadOnlyCollection<string> InitWorkingDirectory(string topicName, byte partitions)
    {
        
        var baseDirectory = FileLogSegment.GetEventLoggingRoot();

        var workingDirectory = Path.GetFullPath(Path.Combine(baseDirectory, topicName));
        if (!Directory.Exists(workingDirectory))
        {
            Directory.CreateDirectory(workingDirectory);
        }

        var existingFolders = Directory.GetDirectories(workingDirectory).Select(v => new DirectoryInfo(v)).ToArray();
        var wantedFolders = Enumerable.Range(0, partitions).Select(v => new FileLogSegmentRoot((byte)v, Path.Combine(workingDirectory, v.ToString())))
            .ToArray();

        if (!existingFolders.Any())
        {
            Array.ForEach(wantedFolders, item => Directory.CreateDirectory(item.Path));
            return wantedFolders.Select(v => v.Path).ToArray();
        }

        var validFolders = existingFolders
            .Select(v => byte.TryParse(v.Name, out var partitionIdResult) ? new FileLogSegmentRoot(partitionIdResult, v.FullName) : null)
            .Where(v => v is not null).ToArray();

        var missingFolders = wantedFolders.Except(validFolders).ToArray();
        if (missingFolders.Length == 0)
        {
            return validFolders.Select(v => v!.Path).ToArray();
        }

        Array.ForEach(missingFolders, item => Directory.CreateDirectory(item!.Path));

        return validFolders.Concat(missingFolders).OrderBy(v => v!.PartitionId).Select(v => v!.Path).ToArray();
    }
    
    public void CleanupWorkingDirectory(string topicName)
    {
        var baseDirectory = FileLogSegment.GetEventLoggingRoot();

        var workingDirectory = Path.GetFullPath(Path.Combine(baseDirectory, topicName));
        if (!Directory.Exists(workingDirectory))
        {
            return;
        }

        Directory.Delete(workingDirectory, true);
    }
}