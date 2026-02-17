using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventConsumer<TEvent> : IEventConsumer where TEvent : class
{
    Task Broadcast(LogResponse<TEvent> response, CancellationToken cancellationToken = default);
}



public interface IEventConsumer : IEventLogLite
{
    void AssignSegment(LogSegment segment);
    ConsumerAssigmentState GetConsumerAssigmentState();
    
    EventConsumerSettings GetConsumerSettings();
}