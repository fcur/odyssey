using Odyssey.HRMS.EventLogLite.Base;

namespace Odyssey.HRMS.EventLogLite.Producer;

public sealed class EventProducerSettings : EventLogBaseSettings
{
    // public string TopicName { get; set; } = null!;
    public uint FileSizeLimitBytes { get; set; } = 1000_000;
    public byte Partitions { get; set; } = 1;
}

public sealed class EventBrokerSettings : EventLogBaseSettings
{
    public uint FileSizeLimitBytes { get; set; } = 1000;
    public byte Partitions { get; set; } = 50;
    public int DegreeOfParallelism { get; set; } = 16;
}


public sealed class ProducerSettings
{
    public string KeySerializer { get; set; }
    public string ValueSerializer { get; set; }
    public string Acks { get; set; }
    public string[] TargetTopics { get; set; }
    
    public int BatchSize { get; set; } = 10_000;
    public int BatchInterval { get;set; } = 100;
    
}