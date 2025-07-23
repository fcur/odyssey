using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventConsumer<TEvent> : IEventConsumer where TEvent : class
{
    Task Broadcast(LogResponse<TEvent> response, CancellationToken cancellationToken = default);
    
}

public interface IEventConsumer : IEventLogLite
{
    byte GetIndex();
    string GetGroupName();
    /// <summary>
    /// Avoid calling this method frequently due to blocking for concurrent dictionary.
    /// </summary>
    /// <returns>Count of assigned segments</returns>
    int GetSegmentsCount();
    void AssignSegment(LogSegment segment);
}