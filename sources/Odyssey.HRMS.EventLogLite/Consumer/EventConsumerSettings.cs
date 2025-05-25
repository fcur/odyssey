using Odyssey.HRMS.EventLogLite.Base;

namespace Odyssey.HRMS.EventLogLite.Consumer;

public sealed class EventConsumerSettings(string groupName) : EventLogBaseSettings
{
    public string EventName { get; set; } = null!;
    
    public string GroupName { get; set; } = groupName;

    public byte ReplicaCount { get; set; } = 1;

    public int Capacity { get; set; } = 1000;
}
