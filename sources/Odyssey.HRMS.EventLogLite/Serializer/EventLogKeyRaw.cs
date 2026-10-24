using System.Buffers;

namespace Odyssey.HRMS.EventLogLite.Serializer;

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