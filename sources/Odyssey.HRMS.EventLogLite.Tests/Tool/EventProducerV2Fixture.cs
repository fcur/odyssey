using Microsoft.Extensions.Logging;
using Moq;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Producer;
using Odyssey.HRMS.EventLogLite.Tests.Logging;
using System.Diagnostics.CodeAnalysis;
using Xunit.Abstractions;

namespace Odyssey.HRMS.EventLogLite.Tests.Tool;

[ExcludeFromCodeCoverage]
public sealed class EventProducerV2Fixture : IAsyncLifetime
{
    private const string BaseDirectoryRoot = "../../../../../JsonFileEventLoggerTests";
    private const string KeyStringSerializerType = "StringSerializer";
    private const string ValueJsonSerializerType = "JsonRowSerializer";
    public const string Topic1Name = "Topic1";
    public const string Topic2Name = "Topic2";
    public const string Topic3Name = "Topic3";
    public const string Topic4Name = "Topic4";

    private readonly string[] _targetTopics = [Topic1Name, Topic2Name, Topic3Name, Topic4Name];

    static EventProducerV2Fixture()
    {
        LogSegmentDirectory.SetEventLoggingRoot(BaseDirectoryRoot);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public EventProducerV2 GetProducer(ILoggerFactory loggerFactory)
    {
        var testLogger = loggerFactory.CreateLogger<EventProducerV2>();
        var producerSettings = new ProducerSettings 
        { 
            TargetTopics = _targetTopics, 
            KeySerializer = KeyStringSerializerType, 
            ValueSerializer  = ValueJsonSerializerType
        };
        
        var serializerBuilder = new EventLogSerializerBuilder();

        IEventLogKeySerializer keySerializer = producerSettings.KeySerializer switch
        {
            KeyStringSerializerType => new EventLogUtf8StringKeySerializer(),
            _ => throw new NotImplementedException()
        };
        IEventLogMessageSerializer payloadSerializer = producerSettings.ValueSerializer switch
        {
            ValueJsonSerializerType => new JsonEventLogMessageSerializer(),
            _ => throw new NotImplementedException()
        };
        
        var serializer = serializerBuilder.WithKeySerializer(keySerializer).WithMessageSerializer(payloadSerializer).Build();
        
        // var brokerLogger = _loggerFactory.CreateLogger<FileEventLogBroker>();
        // var brokerSettings = new EventBrokerSettings
        // {
        //     TopicName = nameof(EventBrokerSettings.TopicName), Partitions = 42
        // };
        // var eventLoggerMock = new Mock<IFileEventLogger>();
        // var offsetLoggerMock = new Mock<IFileEventLogger>();
        // var broker = new FileEventLogBroker(brokerLogger, brokerSettings, eventLoggerMock.Object, offsetLoggerMock.Object);

        var brokerMock = new Mock<IEventProducerBroker>();
        brokerMock.Setup(v => v.GetMetadata(It.IsAny<MetadataRequest>())).Returns(GetMetadataResponse);

        return new EventProducerV2(testLogger, brokerMock.Object, serializer, producerSettings, new Partitioner());
    }

    private MetadataResponse GetMetadataResponse()
    {
        const int leaderId = 112;
        const int leaderEpoch = 1234;
        var partitions = Enumerable.Range(0, 42).Select(v => new PartitionInfo((byte)v, leaderId, leaderEpoch)).ToArray();
        var topics = _targetTopics.Select(v => new TopicInfo(v, 0, partitions)).ToArray();
        return new MetadataResponse(topics);
    }
}