using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Serializer;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Producer;

public sealed class EventBuffer: IAsyncDisposable
{
    private readonly EventMemoryPool _memoryPool;
    private readonly ConcurrentDictionary<EventBatchKey, PartitionBatch> _batches = new();
    private readonly Channel<PartitionBatch> _readyChannel = Channel.CreateUnbounded<PartitionBatch>(
        new UnboundedChannelOptions { SingleReader = true });
    public ChannelReader<PartitionBatch> ReadyBatches => _readyChannel.Reader;

    public EventBuffer(EventMemoryPool memoryPool)
    {
        _memoryPool = memoryPool;
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
            // 1. Take the current batch  16Kb or create a new one from the 32 MB pool
            BatchWriter WriterFactory() => new(_memoryPool.RentSlice(), _memoryPool);
            // skip batch item properties using WrittenCount value
            var writer = partitionBatch.GetOrAddWriter(WriterFactory);
            var startingWrittenCount = partitionBatch.BatchLength;

            var isNewBatch = startingWrittenCount == 0;

            var batchItem = new LogMessageBatchItem
            {
                RecordLength = 0, // TBS
                Attributes = 0, // TBD
                OffsetDelta = partitionBatch.LastOffsetDelta + 1,
                TimestampDelta = partitionBatch.BaseTimestamp - appendTime,
                KeyLength = serializedKey.Length,
                PayloadLength = 0, // TBS
                MetadataLength = 0, // TBS
                Metadata = metadata
            };

            // 2. Serialize directly to the batch buffer
            var bytesWritten = messageSerializer.TrySerialize(batchItem, serializedKey, data, writer);
            if (bytesWritten > 0)
            {
                partitionBatch.Update(batchItem.OffsetDelta, appendTime, bytesWritten);
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
            bytesWritten = messageSerializer.TrySerialize(batchItem, serializedKey, data, writer);;
            if (bytesWritten > 0)
            {
                partitionBatch.Update(batchItem.OffsetDelta, appendTime, bytesWritten);
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