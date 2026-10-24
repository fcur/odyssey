using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;
using Odyssey.HRMS.EventLogLite.Tests.Logging;
using Odyssey.HRMS.EventLogLite.Tests.Tool;
using System.Diagnostics.CodeAnalysis;
using Xunit.Abstractions;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
public sealed class EventProducerV2Tests: IAsyncLifetime, IClassFixture<EventProducerV2Fixture>
{
    private readonly EventProducerV2Fixture _fixture;
    private readonly ILoggerFactory  _loggerFactory;
    
    // ReSharper disable once ConvertToPrimaryConstructor
    public EventProducerV2Tests(EventProducerV2Fixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(outputHelper)).SetMinimumLevel(LogLevel.Trace);
        });
    }

    [Fact]
    public async Task Publish_Should_SaveTo_MemoryBuffer()
    {
        var producer = _fixture.GetProducer(_loggerFactory);
        await producer.Start();

        var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const byte partition1 = 1;
        const byte partition2 = 2;
        var key1 = Guid.NewGuid().ToString();
        var key2 = Guid.NewGuid().ToString();
        var payload1 = new EventP1 { Timestamp = time };
        var payload2 = new EventP2 { Timestamp = time + 131 };
        var logRequest1 = new LogRequest<string, EventP1>
        {
            TopicName = EventProducerV2Fixture.Topic1Name, Key = key1, Payload = payload1, PartitionId = partition1
        };
        var logRequest2 = new LogRequest<string, EventP2>
        {
            TopicName = EventProducerV2Fixture.Topic2Name, Key = key2, Payload = payload2, PartitionId = partition2
        };

        await producer.Publish(logRequest1, CancellationToken.None);
        await producer.Publish(logRequest2, CancellationToken.None);
    }
    
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
    
    private sealed record EventP1: BaseBatchItem
    {
        
    }
    
    private sealed record EventP2: BaseBatchItem
    {
        
    }
}