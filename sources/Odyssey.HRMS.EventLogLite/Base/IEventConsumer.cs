using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventConsumer<TEvent> : IEventConsumer where TEvent : class
{
    Task Broadcast(LogRespone<TEvent> response, CancellationToken cancellationToken = default);
    
}

public interface IEventConsumer : IEventLogLite
{
    byte GetIndex();
    string GetGroupName();

    void AssignSegment(LogSegment segment);
}