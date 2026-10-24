using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.EventLogLite.Serializer;

public sealed class CompositeEventLogSerializer: IEventLogSerializer
{
    private readonly IEventLogKeySerializer _keySerializer;
    private readonly IEventLogMessageSerializer _messageSerializer;

    public CompositeEventLogSerializer(IEventLogKeySerializer keySerializer, IEventLogMessageSerializer messageSerializer)
    {
        _keySerializer = keySerializer;
        _messageSerializer = messageSerializer;
    }

    public EventLogKeyRaw SerializeKey<TKey>(TKey key)
    {
        return _keySerializer.SerializeKey(key);
    }

    public int TrySerializeBatchItem<TData>(LogMessageBatchItem<byte[], TData> item, BatchWriter writer) where TData : class
    {
        return _messageSerializer.TrySerializeBatchItem(item, writer);
    }

    public int TrySerializeBatchInfo<TData>(LogMessageBatch<byte[], TData> item, BatchWriter writer) where TData : class
    {
        return _messageSerializer.TrySerializeBatchInfo(item, writer);
    }

    public int TrySerialize<TData>(LogMessageBatchItem item, ReadOnlyMemory<byte> rawKey, TData payload, BatchWriter writer) where TData : class
    {
        return _messageSerializer.TrySerialize(item, rawKey, payload, writer);
    }
}