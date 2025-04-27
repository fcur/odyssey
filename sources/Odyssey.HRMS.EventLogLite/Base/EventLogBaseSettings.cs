namespace Odyssey.HRMS.EventLogLite.Base;

public abstract class EventLogBaseSettings
{
    public string TopicName { get; set; } = null!;

    internal string GetTopicWorkingDirectory()
    {
        return Path.Combine(Environment.CurrentDirectory, TopicName);
    }
}