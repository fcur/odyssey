namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventLogLite
{
    void EnsureTopicDirectoryExists();
    Task Start(CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
}