using Odyssey.HRMS.EventLogLite.Base;

namespace Odyssey.HRMS.EventLogLite.Consumer;

public sealed class EventConsumerSettings(string groupName) : EventLogBaseSettings
{
    public string EventName { get; set; } = null!;
    
    public string GroupName { get; set; } = groupName;
}
