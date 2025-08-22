using Microsoft.Extensions.Logging;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Producer;
using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class FileLogBrokerFixture:  IAsyncLifetime
{
    private const string TopicName = "test";
    private const byte Partitions = 5;
    private const string OffsetsTopic =  "__consumer_offsets";
    
    private readonly FileEventLogBroker<TestEvent> _broker;

    public FileLogBrokerFixture()
    {
        var brokerLoggerMock = new Mock<ILogger<FileEventLogBroker<TestEvent>>>();
        var brokerSettings = new EventBrokerSettings { TopicName = OffsetsTopic, Partitions = Partitions };
        var topic = new EventLogTopic(TopicName, Partitions);
        var eventLoggerMock = new Mock<IFileEventLogger>();
        
        _broker = new FileEventLogBroker<TestEvent>(brokerLoggerMock.Object, brokerSettings, eventLoggerMock.Object, topic);

    }
    
    public FileEventLogBroker<TestEvent> GetBroker() => _broker;
    
    
    public Task InitializeAsync()
    {
        return _broker.Start(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        return _broker.Stop(CancellationToken.None);
    }
}