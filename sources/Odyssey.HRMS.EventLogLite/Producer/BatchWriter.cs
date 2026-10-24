using System.Buffers;

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