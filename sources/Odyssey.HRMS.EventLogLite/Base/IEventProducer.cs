using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventProducer<TEvent> : IEventProducer where TEvent : class
{
    ValueTask Publish(LogRequest<TEvent> request, CancellationToken cancellationToken);

    ValueTask Publish(string key, TEvent @event, CancellationToken cancellationToken)
    {
        var request = new LogRequest<TEvent>() { Key = key, Payload = @event, PartitionId = 0 };
        return Publish(request, cancellationToken);
    }
    
}

public interface IEventProducer : IEventLogLite
{
}