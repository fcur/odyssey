using System.Collections.Concurrent;

namespace Odyssey.HRMS.EventLogLite.Producer;

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