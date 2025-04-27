using Odyssey.HRMS.EventLogLite.Base;

namespace Odyssey.HRMS.EventLogLite.Consumer;

public sealed class EventConsumer<TEvent>(EventConsumerSettings settings) : IEventConsumer<TEvent> where TEvent : class
{
    private readonly string _topicWorkingDirectory = settings.GetTopicWorkingDirectory();

    public Task Start(CancellationToken cancellationToken = default)
    {
        EnsureTopicDirectoryExists();
        
        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void EnsureTopicDirectoryExists()
    {
        Directory.CreateDirectory(_topicWorkingDirectory);
    }
}