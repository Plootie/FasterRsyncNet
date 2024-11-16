using System.Runtime.InteropServices;

namespace FasterRsyncNet.Chunk;

[StructLayout(LayoutKind.Sequential)]
public readonly struct ChunkSignature(long startOffset, short length, byte[] hash)
{
    public long StartOffset { get; init; } = startOffset;
    public short Length { get; init; } = length;
    public byte[] Hash { get; init; } = hash;

    public static int ChunkSize => sizeof(short);
}