namespace Odyssey.HRMS.EventLogLite.Entities;

public sealed class LogRequest<TEvent> where TEvent : class
{
    public string Key { get; set; }
    public TEvent Payload { get; set; }
    public int PartitionId { get; set; } = 0;
}