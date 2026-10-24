namespace Odyssey.HRMS.EventLogLite.Producer;

public interface IMemoryPoolSeeder
{
    Memory<byte> RentSlice();
    ValueTask<Memory<byte>> RentSliceAsync(CancellationToken ct = default);
}