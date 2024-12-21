using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FasterRsyncNet.Core;
using FasterRsyncNet.Signature;

namespace FasterRsyncNet.Hash.HashingAlgorithms.Rolling;

public class Adler32 : IRollingChecksum
{
    private const ushort Base = 65521;
    private const int BlockSize = 32;
    private const int MaxBytesPerLoop = 5552;
    private const int MaxBlocksPerLoop = MaxBytesPerLoop / BlockSize;
    
    public RollingChecksumOption RollingChecksumOption => RollingChecksumOption.Adler32;
    public uint CalculateBlock(ReadOnlySpan<byte> block, uint start = 1)
    {
        uint s1 = start & 0xffff;
        uint s2 = start >> 16;

        ref byte @ref = ref MemoryMarshal.GetReference(block);
        nuint index = 0;
        nuint maxIndex = (nuint)(block.Length / BlockSize * BlockSize);

        while (index < maxIndex)
        {
            uint n = (uint)Math.Min(MaxBlocksPerLoop, (maxIndex - index) / BlockSize);

            Vector128<uint> vs1 = Vector128<uint>.Zero;
            Vector128<uint> vs2 = Vector128.Create<uint>([0, 0, 0, s2]);
            Vector128<uint> vps = Vector128.Create<uint>([0, 0, 0, s1 * n]);

            do
            {
                vps += vs1;

                Vector128<byte> bytes = Vector128.LoadUnsafe(ref @ref, index);
                vs1 += Avx2.SumAbsoluteDifferences(bytes, Vector128<byte>.Zero).AsUInt32();
                Vector128<short> mad = Avx2.MultiplyAddAdjacent(bytes,
                    Vector128.Create(32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17));
                vs2 += Avx2.MultiplyAddAdjacent(mad, Vector128.Create(1, 1, 1, 1, 1, 1, 1, 1)).AsUInt32();

                bytes = Vector128.LoadUnsafe(ref @ref, index + 16);
                vs1 += Avx2.SumAbsoluteDifferences(bytes, Vector128<byte>.Zero).AsUInt32();
                mad = Avx2.MultiplyAddAdjacent(bytes,
                    Vector128.Create(16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1));
                vs2 += Avx2.MultiplyAddAdjacent(mad, Vector128.Create(1, 1, 1, 1, 1, 1, 1, 1)).AsUInt32();

                index += BlockSize;
                n--;
            } while (n > 0);

            vs2 += vps << 5;

            s1 = (s1 + Vector128.Sum(vs1)) % Base;
            s2 = (s2 + Vector128.Sum(vs2)) % Base;
        }

        if (index < (nuint)block.Length)
        {
            foreach (byte b in block[(int)index..])
            {
                s1 += b;
                s2 += s1;
            }

            if (s1 > Base) s1 -= Base;
            s2 %= Base;
        }
        
        return s1 | (s2 << 16);
    }

    //TODO: Look at performance optimizations for this
    public uint Rotate(uint checksum, byte remove, byte add, int chunkSize)
    {
        uint s1 = checksum & 0xFFFF;
        uint s2 = checksum >> 16;
        
        s1 = (s1 - remove + add) % Base;
        s2 = (uint)((s2 - (chunkSize * remove) + s1 - 1) % Base);
        
        return s1 | (s2 << 16);
    }
}