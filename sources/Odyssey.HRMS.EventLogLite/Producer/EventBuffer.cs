using Odyssey.HRMS.EventLogLite.Entities;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Producer;

public sealed class BatchWriter: IMemoryOwner<byte>, IBufferWriter<byte>
{
    private readonly Memory<byte> _buffer;
    private readonly IMemoryCollector _memoryCollector;
    public int WrittenCount { get; private set; }

    public BatchWriter(Memory<byte> bufferSlice, IMemoryCollector memoryCollector)
    {
        _buffer = bufferSlice;
        _memoryCollector = memoryCollector;
    }
    
    public int FreeCapacity => _buffer.Length - WrittenCount;

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        var freeMemory = _buffer.Slice(0, WrittenCount);
        if (sizeHint > 0 && freeMemory.Length < sizeHint)
        {
            throw new InsufficientMemoryException($"Insufficient memory wanted: {sizeHint}, free: {freeMemory.Length}");
        }
        return freeMemory; 
    }
    
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        return GetMemory(sizeHint).Span;
    }
    
    // Commit written data
    public void Advance(int count)
    {
        if (count < 0 || WrittenCount + count > _buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        WrittenCount += count;
    }

    public ReadOnlyMemory<byte> GetWrittenData()
    {
        return _buffer.Slice(0, WrittenCount);
    }

    public void Dispose()
    {
        _memoryCollector.ReturnSlice(_buffer);
    }

    public Memory<byte> Memory => _buffer.Slice(0, WrittenCount);
}

public interface IMemoryPoolSeeder
{
    Memory<byte> RentSlice();
    ValueTask<Memory<byte>> RentSliceAsync(CancellationToken ct = default);
}
public interface IMemoryCollector
{
    void ReturnSlice(Memory<byte> slice);
}

public interface IEventMemoryPool : IMemoryPoolSeeder, IMemoryCollector
{
    
}

public sealed class EventMemoryPool: IEventMemoryPool
{
    private readonly byte[] _reservedBuffer;
    private readonly ConcurrentQueue<Memory<byte>> _availableSlices = new();
    private readonly SemaphoreSlim _semaphore;
    
    public int BatchSizeInBytes { get; }
    public int TotalBatches { get; }

    public EventMemoryPool(int totalMemoryMb = 32, int batchSizeKb = 16)
    {
        BatchSizeInBytes = batchSizeKb * 1024; // 16_384 bytes
        int totalMemoryBytes = totalMemoryMb * 1024 * 1024; // 33_554_432 bytes
        TotalBatches = totalMemoryBytes / BatchSizeInBytes; // 2048 batches

        _reservedBuffer = new byte[totalMemoryBytes];
        _semaphore = new SemaphoreSlim(TotalBatches, TotalBatches);
        
        for (var i = 0; i < TotalBatches; i++)
        {
            var offset = i * BatchSizeInBytes;
            // pointer to reserved memory
            var slice = _reservedBuffer.AsMemory(offset, BatchSizeInBytes);
            _availableSlices.Enqueue(slice);
        }
    }
    
    public Memory<byte> RentSlice()
    {
        try
        {        
            // Backpressure
            _semaphore.Wait();

            if (_availableSlices.TryDequeue(out var slice))
            {
                return slice;
            }
        }
        finally
        {
            _semaphore.Release();
        }
        
        throw new InvalidOperationException();
    }
    
    public async ValueTask<Memory<byte>> RentSliceAsync(CancellationToken ct = default)
    {
        try
        {        
            // Backpressure
            await _semaphore.WaitAsync(ct);

            if (_availableSlices.TryDequeue(out var slice))
            {
                return slice;
            }
        }
        finally
        {
            _semaphore.Release();
        }

        throw new InvalidOperationException();
    }

    public void ReturnSlice(Memory<byte> slice)
    {
        _availableSlices.Enqueue(slice);
        _semaphore.Release();
    }
}



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
        LastOffsetDelta = data.OffsetDelta;
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

public sealed class EventBuffer: IAsyncDisposable
{
    private readonly EventMemoryPool _pool;
    private readonly ConcurrentDictionary<EventBatchKey, PartitionBatch> _batches = new();
    private readonly Channel<PartitionBatch> _readyChannel = Channel.CreateUnbounded<PartitionBatch>(
        new UnboundedChannelOptions { SingleReader = true });
    public ChannelReader<PartitionBatch> ReadyBatches => _readyChannel.Reader;

    public EventBuffer(EventMemoryPool memoryPool)
    {
        _pool = memoryPool;
    }

    public void Append<TData>(string topic, byte partitionId, ReadOnlyMemory<byte> serializedKey, TData data, Dictionary<string, object> metadata, IEventLogMessageSerializer messageSerializer) where TData: class
    {
        const int baseSequence = 0; // TBD from counter
        const long producerId = 0; // TBD
        const byte producerEpoch = 0; // TBD
        const int minBatchSize = 64; // TBD
        
        var appendTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var eventBatchKey = new EventBatchKey(topic, partitionId);
        var partitionBatch = _batches.GetOrAdd(eventBatchKey, _ => new PartitionBatch(appendTime, baseSequence, producerId, producerEpoch ));

        using (partitionBatch.EnterLock())
        {
            // 1. Take the current batch or create a new one from the 32 MB pool
            BatchWriter WriterFactory() => new(_pool.RentSlice(), _pool);
            // skip batch item properties using WrittenCount value
            var writer = partitionBatch.GetOrAddWriter(WriterFactory);
            var startingWrittenCount = partitionBatch.BatchLength;

            var isNewBatch = startingWrittenCount == 0;

            var batchItem = new LogMessageBatchItem<byte[], TData>
            {
                RecordLength = 0, // TBS
                Attributes = 0, // TBD
                OffsetDelta = partitionBatch.LastOffsetDelta + 1,
                TimestampDelta = partitionBatch.BaseTimestamp - appendTime,
                KeyLength = serializedKey.Length,
                Key = serializedKey.Span,
                PayloadLength = 0, // TBS
                Payload = data,
                MetadataLength = 0, // TBS
                Metadata = metadata
            };

            // 2. Serialize directly to the batch buffer
            var bytesWritten = messageSerializer.TrySerializeBatchItem(batchItem, writer);
            if (bytesWritten > 0)
            {
                partitionBatch.Update(batchItem, appendTime, bytesWritten);
            }
            // 3. If it doesn't fit current buffer, we send the existing data
            else if (startingWrittenCount > 0)
            {
                // TODO: serialize batch info
                _readyChannel.Writer.TryWrite(partitionBatch);
            }
            
            // 4. Attempt to write into a new batch
            // check available batch size (16Kb) and throw exception for data large that message max bytes (1Mb)

            writer = WriterFactory();
            partitionBatch.Reset(appendTime, baseSequence + 1, writer);
            bytesWritten = messageSerializer.TrySerializeBatchItem(batchItem, writer);
            if (bytesWritten > 0)
            {
                partitionBatch.Update(batchItem, appendTime, bytesWritten);
            }
            // 5. If it doesn't fit a new batch buffer again then throw an exception
            else
            {
                throw new ArgumentException(nameof(data));
            }
            
            // 6. Send almost full batch to the network channel 
            if (writer.FreeCapacity < minBatchSize)
            {
                // TODO: serialize batch info
                _readyChannel.Writer.TryWrite(partitionBatch);
                partitionBatch.Reset(appendTime, baseSequence + 2, null);
            }
        }
        
    }

    public async ValueTask DisposeAsync()
    {
        // TODO release managed resources here
    }
}


public record BaseBatchItem
{
    public long Timestamp { get; set; }
}
