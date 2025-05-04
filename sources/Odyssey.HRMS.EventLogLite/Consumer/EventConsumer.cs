using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Consumer;

public sealed class EventConsumer<TEvent> : IEventConsumer<TEvent> where TEvent : class
{
    private readonly EventConsumerSettings _settings;
    private Func<LogRespone<TEvent>, CancellationToken, Task>? _handler;

    public EventConsumer(EventConsumerSettings settings, Func<LogRespone<TEvent>, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(handler);

        _settings = settings;
        _handler = handler;
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        EnsureTopicDirectoryExists();

        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void ApplyHandler(Func<LogRespone<TEvent>, CancellationToken, Task> handler)
    {
        _handler = handler;
    }

    public void EnsureTopicDirectoryExists()
    {
        var topicWorkingDirectory = _settings.GetTopicWorkingDirectory();
        Directory.CreateDirectory(topicWorkingDirectory);
    }
}