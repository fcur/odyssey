using Microsoft.Extensions.Logging;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Producer;
using System.Diagnostics.CodeAnalysis;

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

    public void CreateDirectories(string workingDirectory, params string[] folders)
    {
        foreach (var item in folders)
        {
            var newFolder = Path.Combine(workingDirectory, item);
            Directory.CreateDirectory(newFolder);
        }
    }

    public IReadOnlyCollection<string> GetFolders(string workingDirectory)
    {
        return Directory.GetDirectories(workingDirectory);
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

        return Task.CompletedTask;
    }
}