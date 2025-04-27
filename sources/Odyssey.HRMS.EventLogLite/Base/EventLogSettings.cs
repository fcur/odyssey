namespace Odyssey.HRMS.EventLogLite.Base;

public static class EventLogSettings
{
    public const string ConfigurationSectionName = "EventLogging";
    public const string ProducerSectionName = "Producer";
    public const string ConsumerSectionName = "Consumer";
    public const string OffsetsFileName = "__offsets.log";
    public const string LogFileExtension = ".log";
}