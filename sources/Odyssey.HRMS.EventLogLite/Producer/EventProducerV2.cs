using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using Odyssey.HRMS.EventLogLite.Serializer;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace Odyssey.HRMS.EventLogLite.Producer;


public sealed class EventLogKeyRaw: IDisposable
{
    private byte[]? _rentedArray;
    public ReadOnlyMemory<byte> Memory { get; private set; } = ReadOnlyMemory<byte>.Empty;
    
    public byte[] RentMemory(int byteCount)
    {
        if (_rentedArray != null)
        {
            throw new InvalidOperationException("Buffer is already rent");
        }

        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), "ByteCount must be positive");
        }

        _rentedArray = ArrayPool<byte>.Shared.Rent(byteCount);
        return _rentedArray;
    }

    public void Commit(int byteCount)
    {
        if (_rentedArray == null)
        {
            throw new InvalidOperationException("Cannot commit a unset array");
        }
        Memory = new ReadOnlyMemory<byte>(_rentedArray, 0, byteCount);
    }
    
    public void Dispose()
    {
        var array = Interlocked.Exchange(ref _rentedArray, null);
        if (array != null)
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }
}

public sealed class Partitioner
{
    private const uint PartitionIdSeed = 42;
    public bool TryExtractPartition(byte? partitionId, out byte result)
    {
        if (!partitionId.HasValue)
        {
            result = 0;
            return false;
        }

        result =  partitionId.Value;
        return true;
    }
    
    public bool TryCalculatePartition(ReadOnlyMemory<byte> serializedKey, int partitionsCount, out byte result)
    {
        if (serializedKey.Length == 0)
        {
            result = 0;
            return false;
        }
        
        // TODO: test MurmurHash3 X 
        // partition = murmur2.hash(key) % numPartitions
        var hash = MurmurHash2.Hash32(serializedKey, PartitionIdSeed);
        result = Convert.ToByte(hash % partitionsCount);
        return true;
    }
}

public readonly struct EventLogUtf8StringKeySerializer : IEventLogKeySerializer
{
    private static readonly Type KeyStringType = typeof(string);
    
    public EventLogKeyRaw SerializeKey<TKey>(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        if (typeof(TKey) != KeyStringType)
        {
            throw new ArgumentException("TKey must be of type string");
        }

        var result = new EventLogKeyRaw();
        var keyString = Unsafe.As<TKey, string>(ref key);
        if (string.IsNullOrEmpty(keyString))
        {
            return result;
        }
        
        var keyLength = Encoding.UTF8.GetByteCount(keyString);
        var rentedArray = result.RentMemory(keyLength);
        
        Encoding.UTF8.GetBytes(keyString.AsSpan(), rentedArray.AsSpan(0, keyLength));
        
        result.Commit(keyLength);
        
        return result;
    }
}


public sealed class EventLogSerializerBuilder
{
    private IEventLogKeySerializer _keySerializer = null!;
    private IEventLogMessageSerializer _messageSerializer = null!;
    

    public EventLogSerializerBuilder WithMessageSerializer(IEventLogMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _messageSerializer = serializer;
        return this;
    }

    public EventLogSerializerBuilder WithKeySerializer(IEventLogKeySerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _keySerializer = serializer;
        return this;
    }
    
    public IEventLogSerializer Build()
    {
        if (_keySerializer is null || _messageSerializer is null)
        {
            throw new InvalidOperationException();
        }

        return new CompositeEventLogSerializer(_keySerializer, _messageSerializer);
    }
}

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


public interface IEventLogSerializer : IEventLogKeySerializer, IEventLogMessageSerializer
{
    
}

public interface IEventLogKeySerializer
{
    EventLogKeyRaw SerializeKey<TKey> (TKey key);
}

public interface IEventLogMessageSerializer
{
    // message key is already serialized into array of bytes
    int TrySerializeBatchItem<TData>(LogMessageBatchItem<byte[], TData> item, BatchWriter writer) where TData : class;
    int TrySerializeBatchInfo<TData>(LogMessageBatch<byte[], TData> item, BatchWriter writer) where TData : class;
    int TrySerialize<TData>(LogMessageBatchItem item, ReadOnlyMemory<byte> rawKey, TData payload, BatchWriter writer) where TData : class;
    
}

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

public sealed class EventProducerV2 : IEventProducerV2, IDisposable
{
    private readonly ILogger<EventProducerV2> _logger;
    private readonly IEventProducerBroker _broker;
    private readonly ProducerSettings _settings;
    private readonly ConcurrentDictionary<TopicName, TopicInfo> _topics = [];

    private readonly EventMemoryPool _memoryPool;
    private readonly EventBuffer  _buffer;
    private readonly IEventLogSerializer _serializer;
    private readonly Partitioner _partitioner;
    
    public EventProducerV2(ILogger<EventProducerV2> logger, IEventProducerBroker broker, IEventLogSerializer serializer, ProducerSettings settings, Partitioner partitioner)
    {
        _logger = logger;
        _broker = broker;
        _settings = settings;
        _memoryPool = new EventMemoryPool();
        _buffer =  new EventBuffer(_memoryPool);
        _serializer = serializer;
        _partitioner = partitioner;
    }

    public void Dispose()
    {
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        var getMetadataRequest = new MetadataRequest(_settings.TargetTopics);
        var metadataResult = _broker.GetMetadata(getMetadataRequest);
        foreach (var topicInfo in metadataResult.Topics)
        {
            var key = new TopicName(topicInfo.Name);
            var info = new TopicInfo((byte)topicInfo.Partitions.Length, 0);
            _topics.AddOrUpdate(key,info, (k, v) => info);
        }
        
        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public EventProducerSettings GetSettings()
    {
        throw new NotImplementedException();
    }

    public ValueTask Publish<TKey, TData>(LogRequest<TKey, TData> request, CancellationToken cancellationToken) where TKey : class where TData : BaseBatchItem
    {
        var topicName = request.TopicName;
        
        using var rawKey = _serializer.SerializeKey(request.Key);
        var serializedKey = rawKey.Memory;
        
        // var serializedKey = request.Key != null ? SerializeKey(request.Key) : null;
        var key = new TopicName(request.TopicName);
        
        if (!_topics.TryGetValue(key, out var topicInfo))
        {
            throw new ArgumentException($"No active topic '{request.TopicName}' found");
        }
        
        var partitionId = _partitioner.TryExtractPartition(request.PartitionId, out var idResult) switch
        {
            true => idResult,
            false when _partitioner.TryCalculatePartition(serializedKey, topicInfo.PartitionsCount, out var keyResult) => keyResult,
            _ => GetNextPartition(key, topicInfo) // TODO: Sticky Partitioning
        };

        _buffer.Append(topicName, partitionId, serializedKey, request.Payload, request.Metadata, _serializer);
        
        
        throw new NotImplementedException();
    }

    // private const uint PartitionIdSeed = 42;

    // public static byte GetPartition<TEvent>(LogRequest<TEvent> request, ActiveTopicInfo? topicInfo = null) where TEvent : class
    // {
    //     if (request.PartitionId.HasValue)
    //     {
    //         return request.PartitionId.Value;
    //     }
    //
    //     byte result;
    //     var key = new ActiveTopicKey(request.TopicName);
    //
    //     while (true)
    //     {
    //         if (!_activeTopicInfos.TryGetValue(key, out var topicInfo))
    //         {
    //             throw new ApplicationException($"No active topic '{request.TopicName}' found");
    //         }
    //
    //         if (!string.IsNullOrEmpty(request.Key))
    //         {
    //             // TODO: test MurmurHash3 X 
    //             // partition = murmur2.hash(key) % numPartitions
    //             var hash = MurmurHash2.Hash32(Encoding.UTF8.GetBytes(request.Key), PartitionIdSeed);
    //             result = Convert.ToByte(hash % topicInfo.PartitionsCount);
    //             break;
    //         }
    //
    //         var nextInfo = topicInfo.NextRoundRobinInfo();
    //         if (!_activeTopicInfos.TryUpdate(key, nextInfo, topicInfo))
    //         {
    //             // another thread was ahead
    //             continue;
    //         }
    //
    //         result = nextInfo.TempPartition;
    //         break;
    //     }
    //
    //     return result;
    // }

    // private byte[] SerializeKey<TKey>(TKey key)
    // {
    //     const string stringSerializerType = "StringSerializer";
    //     return key switch
    //     {
    //         string keyStr when _settings.KeySerializer == stringSerializerType => Encoding.UTF8.GetBytes(keyStr),
    //         _ => throw new SerializationException()
    //     };
    // }
    
    private byte GetNextPartition(TopicName key, TopicInfo topicInfo)
    {
        byte result;
        while (true)
        {
            var nextInfo = topicInfo.NextRoundRobinInfo();
            if (!_topics.TryUpdate(key, nextInfo, topicInfo))
            {
                // another thread was ahead
                continue;
            }

            result = nextInfo.TempPartition;
            break;
        }

        return result;
    }
    
    
    // private byte GetPartition<TKey, TData>(LogRequest<TKey, TData> request) where TKey : class where TData : class
    // {
    //     if (request.PartitionId.HasValue)
    //     {
    //         return request.PartitionId.Value;
    //     }
    //
    //     byte result;
    //     var key = new TopicName(request.TopicName);
    //
    //     if (!_topics.TryGetValue(key, out var topicInfo))
    //     {
    //         throw new ApplicationException($"No active topic '{request.TopicName}' found");
    //     }
    //
    //     if (TrySerializeKey(request.Key, out var serializedKey))
    //     {
    //         // TODO: test MurmurHash3 X 
    //         // partition = murmur2.hash(key) % numPartitions
    //         var hash = MurmurHash2.Hash32(serializedKey, PartitionIdSeed);
    //         result = Convert.ToByte(hash % topicInfo.PartitionsCount);
    //         return result;
    //     }
    //
    //     while (true)
    //     {
    //         var nextInfo = topicInfo.NextRoundRobinInfo();
    //         if (!_topics.TryUpdate(key, nextInfo, topicInfo))
    //         {
    //             // another thread was ahead
    //             continue;
    //         }
    //
    //         result = nextInfo.TempPartition;
    //         break;
    //     }
    //
    //     return result;
    // }ʼ

    private async Task StartSeeding(CancellationToken ct)
    {
        await foreach (var batch in _buffer.ReadyBatches.ReadAllAsync(ct))
        {
            if (batch.ItemsCount==0)
            {
                continue;
            }

            try
            {
                var writtenData = batch.GetWrittenData();
                // save batch to file
            }
            finally
            { 
                // release memory 
                // _memoryPool.ReturnSlice(writer.GetBuffer());

            }
            
        }
    }

    private readonly record struct TopicName(string Value);

    private readonly record struct TopicInfo(byte PartitionsCount, byte TempPartition)
    {
        public TopicInfo NextRoundRobinInfo()
        {
            return this with { TempPartition = (byte)(TempPartition + 1 % PartitionsCount) };
        }
    }
    
}

public readonly record struct EventBatchKey(string Topic, int Partition);

public sealed record MetadataRequest(string[] Topics);

public sealed record MetadataResponse(TopicInfo[] Topics);

public sealed record TopicInfo(string Name, byte Attributes, PartitionInfo[] Partitions);

public sealed record PartitionInfo(byte Index, int LeaderId, int LeaderEpoch);


public sealed record ProduceRequest(ProduceTopic[] Data);
public sealed record ProduceTopic(string Name, ProduceTopicData[] Data);

public sealed record ProduceTopicData(byte PartitionId, ProduceTopicRecord[] Records);

public sealed record ProduceTopicRecord(int BatchOffset, ProduceTopicItem[] Records);

public sealed record ProduceTopicItem(int KeyLength, byte[] Key, int ValueLength, byte[] Value, ProduceItemHeader[]? Headers );

public sealed record ProduceItemHeader(string Key, byte[] Value);
