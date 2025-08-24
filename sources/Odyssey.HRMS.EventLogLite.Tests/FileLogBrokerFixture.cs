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
        FileLogSegment.InitWorkingDirectory(TopicName, Partitions);
        FileLogSegment.InitWorkingDirectory(OffsetsTopic, Partitions);

        // await _broker.Start(CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // await _broker.Stop(CancellationToken.None);

        FileLogSegment.CleanupWorkingDirectory(TopicName);
        FileLogSegment.CleanupWorkingDirectory(OffsetsTopic);

        return Task.CompletedTask;
    }
}