namespace Odyssey.HRMS.EventLogLite.Base;

public sealed class FileLogSegmentRoot(byte partitionId, string path) : IEquatable<FileLogSegmentRoot>
{
    public string Path { get; } = path;

    public byte PartitionId { get; } = partitionId;

    public bool Equals(FileLogSegmentRoot? other)
    {
        if (other is null)
        {
            return false;
        }

        return PartitionId == other.PartitionId;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is FileLogSegmentRoot other && Equals(other);
    }

    public override int GetHashCode()
    {
        return PartitionId;
    }
}