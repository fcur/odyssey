using Odyssey.HRMS.EventLogLite.Base;

namespace Odyssey.HRMS.EventLogLite.Consumer;

public sealed class EventConsumerSettings(string groupName) : EventLogBaseSettings
{
    public string EventName { get; set; } = null!;
    
    public string GroupName { get; set; } = groupName;

    public byte ReplicaCount { get; set; } = 1;

    public int BatchSize { get; set; } = 100;

    public TimeSpan PullDuration { get; set; } = TimeSpan.FromSeconds(5);
    
    public int MaxBytes { get; set; } = 1048576; // 1Mb
    public int MaxWaitTimeMs { get; set; } = 500;
}
