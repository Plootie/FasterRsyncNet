using System.Runtime.InteropServices;

namespace FasterRsyncNet.Chunk;

[StructLayout(LayoutKind.Sequential)]
public readonly struct ChunkSignature(long startOffset, short length, byte[] hash, uint rollingChecksum)
{
    public long StartOffset { get; init; } = startOffset;
    public short Length { get; init; } = length;
    public byte[] Hash { get; init; } = hash;
    public uint RollingChecksum { get; init; } = rollingChecksum;

    public static int ChunkSize => sizeof(short) + sizeof(uint);
}