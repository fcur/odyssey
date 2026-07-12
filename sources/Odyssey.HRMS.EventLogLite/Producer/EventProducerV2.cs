using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text;

namespace Odyssey.HRMS.EventLogLite.Producer;

public sealed class EventProducerV2 : IEventProducerV2, IDisposable
{
    private readonly ILogger<EventProducerV2> _logger;
    private readonly IEventProducerBroker _broker;
    private readonly ProducerSettings _settings;
    private readonly ConcurrentDictionary<TopicName, TopicInfo> _topics = [];

    public EventProducerV2(ILogger<EventProducerV2> logger, IEventProducerBroker broker, ProducerSettings settings)
    {
        _logger = logger;
        _broker = broker;
        _settings = settings;
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
        
        throw new NotImplementedException();
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public EventProducerSettings GetSettings()
    {
        throw new NotImplementedException();
    }

    public ValueTask Publish<TKey, TData>(LogRequest<TKey, TData> request, CancellationToken cancellationToken) where TKey : class where TData : class
    {
        var topicName = request.TopicName;
        var serializedKey = request.Key != null ? SerializeKey(request.Key) : null;
        var key = new TopicName(request.TopicName);
        
        if (!_topics.TryGetValue(key, out var topicInfo))
        {
            throw new ArgumentException($"No active topic '{request.TopicName}' found");
        }
        
        var partitionId = TryGetPartition(request.PartitionId, out var idResult) switch
        {
            true => idResult,
            false when TryGetPartition(serializedKey, topicInfo.PartitionsCount, out var keyResult) => keyResult,
            _ => GetPartition(key, topicInfo)
        };
        var accumulatorKey = new RecordAccumulatorKey(topicName, partitionId);


        throw new NotImplementedException();
    }

    private const uint PartitionIdSeed = 42;

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

    private byte[] SerializeKey<TKey>(TKey key)
    {
        const string stringSerializerType = "StringSerializer";
        return key switch
        {
            string keyStr when _settings.KeySerializer == stringSerializerType => Encoding.UTF8.GetBytes(keyStr),
            _ => throw new SerializationException()
        };
    }
    
    private bool TrySerializeKey<TKey>(TKey key, [NotNullWhen(true)] out byte[]? result)
    {
        const string stringSerializerType = "StringSerializer";

        result = key switch
        {
            string keyStr when _settings.KeySerializer==stringSerializerType => Encoding.UTF8.GetBytes(keyStr),
            _ => null
        };

        return result is not null;
    }


    private byte CalculatePartitionId(byte partitionsCount, byte[] serializedKey)
    {
        var hash = MurmurHash2.Hash32(serializedKey, PartitionIdSeed);
        var result = Convert.ToByte(hash % partitionsCount);
        return result;
    }


    private bool TryGetPartition(byte? partitionId, out byte result)
    {
        if (!partitionId.HasValue)
        {
            result = 0;
            return false;
        }

        result =  partitionId.Value;
        return true;
    }

    private bool TryGetPartition(byte[]? serializedKey, int partitionsCount, out byte result)
    {
        if (serializedKey is null || serializedKey.Length == 0)
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

    private byte GetPartition(TopicName key, TopicInfo topicInfo)
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
    // }

    private readonly record struct TopicName(string Value);

    private readonly record struct TopicInfo(byte PartitionsCount, byte TempPartition)
    {
        public TopicInfo NextRoundRobinInfo()
        {
            return this with { TempPartition = (byte)(TempPartition + 1 % PartitionsCount) };
        }
    }
    
}

public readonly record struct RecordAccumulatorKey(string Topic, int Partition);

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

public sealed class RecordAccumulator : IAsyncDisposable
{
    // private readonly ConcurrentDictionary<RecordAccumulatorKey, >
    public async ValueTask DisposeAsync()
    {
        // TODO release managed resources here
    }
}