using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
        var partitionId = GetPartition(request);


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

    private byte GetPartition<TKey, TData>(LogRequest<TKey, TData> request) where TKey : class where TData : class
    {
        if (request.PartitionId.HasValue)
        {
            return request.PartitionId.Value;
        }

        byte result;
        var key = new TopicName(request.TopicName);

        if (!_topics.TryGetValue(key, out var topicInfo))
        {
            throw new ApplicationException($"No active topic '{request.TopicName}' found");
        }

        if (TrySerializeKey(request.Key, out var serializedKey))
        {
            // TODO: test MurmurHash3 X 
            // partition = murmur2.hash(key) % numPartitions
            var hash = MurmurHash2.Hash32(serializedKey, PartitionIdSeed);
            result = Convert.ToByte(hash % topicInfo.PartitionsCount);
            return result;
        }

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