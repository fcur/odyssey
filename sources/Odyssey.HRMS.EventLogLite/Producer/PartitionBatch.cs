using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.EventLogLite.Producer;

public sealed class PartitionBatch
{
    private readonly Lock _lock = new();
    private BatchWriter? _items;
    
    public long BaseTimestamp { get; private set; } // init time
    public long BaseSequence { get; private set; } // sequence number provided by producer 
    public long ProducerId {get; init;}
    public byte ProducerEpoch {get; init;}
    
    public long LastOffsetDelta { get; private set; }
    public long FirstTimestamp { get; private set; }
    public long MaxTimestamp { get; private set; }
    public int ItemsCount { get; private set; }
    
    public int BatchLength => _items?.WrittenCount ?? 0;
    public long LastOffset => BaseSequence + LastOffsetDelta;
    
    public PartitionBatch(long baseTimestamp, long baseSequence, long producerId, byte producerEpoch)
    {
        BaseTimestamp =  baseTimestamp;
        FirstTimestamp = baseTimestamp;
        BaseSequence = baseSequence;
        ProducerId = producerId;
        ProducerEpoch = producerEpoch;
    }
    
    public Lock.Scope EnterLock() => _lock.EnterScope();
    public ReadOnlyMemory<byte>? GetWrittenData() => _items?.GetWrittenData();
    
    public void Update<TKey, TData>(LogMessageBatchItem<TKey, TData> data, long appendTime, int writtenCount) where TKey : class where TData: class
    {
        Update(data.OffsetDelta, appendTime, writtenCount);
    }

    public void Update(long offsetDelta, long appendTime, int writtenCount)
    {
        LastOffsetDelta = offsetDelta;
        ItemsCount++;
        MaxTimestamp = Math.Max(MaxTimestamp, appendTime);
    }
    
    public BatchWriter GetOrAddWriter(Func<BatchWriter> writerFactory)
    {
        if (_items != null)
        {
            return _items;
        }
        
        _items = writerFactory();
        return _items;
    }

    public void Reset(long baseTimestamp, long baseSequence, BatchWriter? writer)
    {
        BaseTimestamp = baseTimestamp;
        BaseSequence = baseSequence;
        _items = writer;
    }
}