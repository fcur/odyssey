namespace Odyssey.HRMS.EventLogLite.Entities;

public sealed class LogMessage<TEvent> where TEvent : class
{
    public string Key { get; set; }
    public TEvent Payload { get; set; }
    public long Timestamp { get; set; }

    public static LogMessage<TEvent> Create(LogRequest<TEvent>  request)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new LogMessage<TEvent> { Key = request.Key, Payload = request.Payload, Timestamp = timestamp };
    }
}