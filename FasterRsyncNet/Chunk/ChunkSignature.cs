using System.Runtime.InteropServices;

namespace FasterRsyncNet.Chunk;

[StructLayout(LayoutKind.Sequential)]
public struct ChunkSignature(long startOffset, short length, byte[] hash, uint rollingChecksum)
{
    public long StartOffset = startOffset;          // 8 (Not saved to disk)
    public short Length = length;                   // 2
    public byte[] Hash = hash;                      // 8
    public uint RollingChecksum = rollingChecksum;  // 4
    //Struct total size: 22 (14 on disk)
}