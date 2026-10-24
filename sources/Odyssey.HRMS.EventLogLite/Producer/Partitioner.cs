using Odyssey.HRMS.EventLogLite.Base;

namespace Odyssey.HRMS.EventLogLite.Producer;

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