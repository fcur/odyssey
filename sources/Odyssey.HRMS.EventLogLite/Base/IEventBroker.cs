using Odyssey.HRMS.EventLogLite.Entities;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices.JavaScript;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventBroker: IEventLogLite
{
}

public interface IEventBroker<TEvent>: IEventBroker where TEvent : class
{
    Task<EventLogOffset> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default);

    void Join(IEventConsumer<TEvent> consumer, CancellationToken cancellationToken = default);
}


public sealed record EventLogOffset(long Value);

public sealed record EventLogTopic(string Value, byte Partitions);