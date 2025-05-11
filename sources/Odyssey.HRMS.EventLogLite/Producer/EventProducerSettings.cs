using Odyssey.HRMS.EventLogLite.Base;

namespace Odyssey.HRMS.EventLogLite.Producer;

public sealed class EventProducerSettings : EventLogBaseSettings
{
    // public string TopicName { get; set; } = null!;
    public uint FileSizeLimitBytes { get; set; } = 1000_000;
    public byte Partitions { get; set; } = 1;
}