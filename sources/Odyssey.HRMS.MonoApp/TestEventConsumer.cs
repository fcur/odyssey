using Odyssey.HRMS.EventLogLite;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.MonoApp;

public sealed class TestEventConsumer1 : IEventConsumerImpl<TestEvent>
{
    private readonly ILogger<TestEventConsumer1> _logger;

    public const string GroupName = "Test1";
    
    public TestEventConsumer1(ILogger<TestEventConsumer1> logger)
    {
        _logger = logger;
    }

    public Task Handle(LogRespone<TestEvent> message, CancellationToken cancellationToken)
    {
        var payload = message.Payload;
        var offset = message.Offset;
        var timestamp = message.Timestamp;

        _logger.LogInformation("New event occured: '{EventMessage}' at '{Time}' with offset '{Offset}' for {HandlerName}", payload, timestamp, offset,
            nameof(TestEventConsumer1));
        return Task.CompletedTask;
    }
}

public sealed class TestEventConsumer2 : IEventConsumerImpl<TestEvent>
{
    private readonly ILogger<TestEventConsumer1> _logger;

    public const string GroupName = "Test2";
    
    public TestEventConsumer2(ILogger<TestEventConsumer1> logger)
    {
        _logger = logger;
    }

    public Task Handle(LogRespone<TestEvent> message, CancellationToken cancellationToken)
    {
        var payload = message.Payload;
        var offset = message.Offset;
        var timestamp = message.Timestamp;

        _logger.LogInformation("New event occured: '{EventMessage}' at '{Time}' with offset '{Offset}' for {HandlerName}", payload, timestamp, offset,
            nameof(TestEventConsumer2));
        return Task.CompletedTask;
    }
}

