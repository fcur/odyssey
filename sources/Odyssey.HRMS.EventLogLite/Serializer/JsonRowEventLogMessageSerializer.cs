using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.EventLogLite.Serializer;

public readonly struct JsonRowEventLogMessageSerializer: IEventLogMessageSerializer
{
    public int TrySerializeBatchItem<TData>(LogMessageBatchItem<byte[], TData> item, BatchWriter writer) where TData : class
    {
        return LogSerializer.TrySerializeJsonRow(writer, item);
    }

    public int TrySerializeBatchInfo<TData>(LogMessageBatch<byte[], TData> item, BatchWriter writer) where TData : class
    {
        throw new NotImplementedException();
    }

    public int TrySerialize<TData>(LogMessageBatchItem item, ReadOnlyMemory<byte> rawKey, TData payload, BatchWriter writer) where TData : class
    {
        throw new NotImplementedException();
    }
}