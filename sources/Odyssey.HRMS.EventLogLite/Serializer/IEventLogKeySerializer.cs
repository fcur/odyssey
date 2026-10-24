namespace Odyssey.HRMS.EventLogLite.Serializer;

public interface IEventLogKeySerializer
{
    EventLogKeyRaw SerializeKey<TKey> (TKey key);
}