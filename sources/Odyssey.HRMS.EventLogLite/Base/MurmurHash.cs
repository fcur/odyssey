using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Odyssey.HRMS.EventLogLite.Base;

public static class MurmurHash2
{
    public static uint Hash32(ReadOnlyMemory<byte> data, uint seed = 0) 
        => Hash32(data.Span, seed);
    
    public static uint Hash32(byte[] data, uint seed = 0) 
        => Hash32(data.AsSpan(), seed);
    
    private static uint Hash32(ReadOnlySpan<byte> data, uint seed = 0)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;

        var length = data.Length;
        var h1 = seed;
        var blocks = length / 4;

        var uintBlocks = MemoryMarshal.Cast<byte, uint>(data[..(blocks * 4)]);

        for (var i = 0; i < blocks; i++)
        {
            var k1 = uintBlocks[i];

            k1 *= c1;
            k1 = RotateLeft(k1, 15);
            k1 *= c2;

            h1 ^= k1;
            h1 = RotateLeft(h1, 13);
            h1 = h1 * 5 + 0xe6546b64;
        }

        // Process remaining bytes
        uint tail = 0;
        var tailIndex = blocks * 4;

        switch (length & 3)
        {
            case 3:
                tail ^= (uint)data[tailIndex + 2] << 16;
                goto case 2;
            case 2:
                tail ^= (uint)data[tailIndex + 1] << 8;
                goto case 1;
            case 1:
                tail ^= data[tailIndex];
                tail *= c1;
                tail = RotateLeft(tail, 15);
                tail *= c2;
                h1 ^= tail;
                break;
        }

        h1 ^= (uint)length;
        h1 = Finalize(h1);

        return h1;
    }
    
    // public static uint Hash32(byte[] data, uint seed = 0)
    // {
    //     const uint c1 = 0xcc9e2d51;
    //     const uint c2 = 0x1b873593;
    //     uint length = (uint)data.Length;
    //     uint h1 = seed;
    //     for (int i = 0; i < length / 4; i++)
    //     {
    //         uint k1 = BitConverter.ToUInt32(data, i * 4);
    //         k1 *= c1;
    //         k1 = RotateLeft(k1, 15);
    //         k1 *= c2;
    //         h1 ^= k1;
    //         h1 = RotateLeft(h1, 13);
    //         h1 = h1 * 5 + 0xe6546b64;
    //     }
    //     // Process remaining bytes
    //     uint tail = 0;
    //     switch (length & 3)
    //     {
    //         case 3: tail ^= (uint)data[length - 3] << 16; break;
    //         case 2: tail ^= (uint)data[length - 2] << 8; break;
    //         case 1: tail ^= (uint)data[length - 1]; break;
    //     }
    //     
    //     h1 ^= tail;
    //     h1 ^= length;
    //     h1 = Finalize(h1);
    //     return h1;
    // }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint value, int shift)
    {
        return (value << shift) | (value >> (32 - shift));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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