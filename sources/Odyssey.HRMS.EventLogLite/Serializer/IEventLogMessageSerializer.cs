using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.EventLogLite.Serializer;

public interface IEventLogMessageSerializer
{
    // message key is already serialized into array of bytes
    int TrySerializeBatchItem<TData>(LogMessageBatchItem<byte[], TData> item, BatchWriter writer) where TData : class;
    int TrySerializeBatchInfo<TData>(LogMessageBatch<byte[], TData> item, BatchWriter writer) where TData : class;
    int TrySerialize<TData>(LogMessageBatchItem item, ReadOnlyMemory<byte> rawKey, TData payload, BatchWriter writer) where TData : class;
    
}