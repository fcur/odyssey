using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;

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

public interface IEventProducerV2 : IEventProducer
{
    ValueTask Publish<TKey, TData>(LogRequest<TKey, TData> request, CancellationToken cancellationToken) where TKey : class where TData : class;
    ValueTask Publish<TEvent>(string key, TEvent @event, CancellationToken cancellationToken) where TEvent : class
    {
        var request = new LogRequest<string, TEvent>() { Key = key, Payload = @event, PartitionId = 0 };
        return Publish<string, TEvent>(request, cancellationToken);
    }
}


public interface IEventProducer : IEventLogLite
{
    EventProducerSettings GetSettings();
}