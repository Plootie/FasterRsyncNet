using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FasterRsyncNet.Core;
using FasterRsyncNet.Signature;

namespace FasterRsyncNet.Hash.HashingAlgorithms.Rolling;

public class Adler32(short windowSize) : IRollingChecksum
{
    private const ushort Base = 65521;
    private const int BlockSize = 32;
    private const int MaxBytesPerLoop = 5552;
    private const int MaxBlocksPerLoop = MaxBytesPerLoop / BlockSize;
    private readonly RingBuffer<byte> _byteWindow = new((uint)windowSize);

    public short WindowSize { get; init; } = windowSize;
    public RollingChecksumOption RollingChecksumOption => RollingChecksumOption.Adler32;

    public uint Append(ReadOnlySpan<byte> block)
    {
        if (block.Length > WindowSize)
        {
            //TODO: Benchmark this against simply looping over the span and enqueuing the bytes one by one
            byte[] slicedBlock = block.Slice(block.Length - WindowSize, WindowSize).ToArray();
            _byteWindow.Add(slicedBlock);
        }
        else
        {
            foreach (byte b in block) _byteWindow.Add(b);
        }

        return GetChecksum();
    }

    public void Append(byte add)
    {
        _byteWindow.Add(add);
    }

    public void Reset()
    {
        _byteWindow.Clear();
    }

    /*TODO: A lot of debate has gone into whether the calculation should go in append or GetChecksum with an exposed
     * Adler/Checksum property that live updates. This decision should be revisited later
     */
    public uint GetChecksum()
    {
        ReadOnlySpan<byte> block = _byteWindow.ToArray();
        //Note: In our use case we will never need to start from anywhere but 1 due to recomputing the checksum
        uint s1 = 1 & 0xffff;
        uint s2 = 0U;

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
}