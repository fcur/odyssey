namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventLogLite
{
    Task Start(CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
}