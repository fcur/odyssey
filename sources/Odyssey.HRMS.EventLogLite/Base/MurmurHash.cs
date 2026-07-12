namespace Odyssey.HRMS.EventLogLite.Base;

public static class MurmurHash2
{
    public static uint Hash32(byte[] data, uint seed = 0)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;
        uint length = (uint)data.Length;
        uint h1 = seed;
        for (int i = 0; i < length / 4; i++)
        {
            uint k1 = BitConverter.ToUInt32(data, i * 4);
            k1 *= c1;
            k1 = RotateLeft(k1, 15);
            k1 *= c2;
            h1 ^= k1;
            h1 = RotateLeft(h1, 13);
            h1 = h1 * 5 + 0xe6546b64;
        }
        // Process remaining bytes
        uint tail = 0;
        switch (length & 3)
        {
            case 3: tail ^= (uint)data[length - 3] << 16; break;
            case 2: tail ^= (uint)data[length - 2] << 8; break;
            case 1: tail ^= (uint)data[length - 1]; break;
        }
        
        h1 ^= tail;
        h1 ^= length;
        h1 = Finalize(h1);
        return h1;
    }
    private static uint RotateLeft(uint value, int shift)
    {
        return (value << shift) | (value >> (32 - shift));
    }
    private static uint Finalize(uint h1)
    {
        h1 ^= h1 >> 16;
        h1 *= 0x85ebca6b;
        h1 ^= h1 >> 13;
        h1 *= 0xc2b2ae35;
        h1 ^= h1 >> 16;
        return h1;
    }
}