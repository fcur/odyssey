using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Consumer;

public abstract class EventConsumerImpl<TEvent> : IEventConsumerImpl<TEvent> where TEvent : class
{
    protected abstract Task HandleImpl(LogResponse<TEvent> message, CancellationToken cancellationToken);

    public Task Handle(LogResponse<TEvent> message, CancellationToken cancellationToken)
    {
        return HandleImpl(message, cancellationToken);
    }
}

public interface IEventConsumerImpl<TEvent> : IEventConsumerImpl where TEvent : class
{
    public Task Handle(LogResponse<TEvent> message, CancellationToken cancellationToken);
}

public interface IEventConsumerImpl {  }