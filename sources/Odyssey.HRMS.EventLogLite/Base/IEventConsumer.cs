namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventConsumer<TEvent> : IEventConsumer where TEvent : class
{
}

public interface IEventConsumer : IEventLogLite
{
}