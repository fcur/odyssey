namespace Odyssey.HRMS.EventLogLite.Producer;

public interface IMemoryCollector
{
    void ReturnSlice(Memory<byte> slice);
}