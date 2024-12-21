namespace FasterRsyncNet.Hash;

public interface IRollingChecksum
{
    public RollingChecksumOption RollingChecksumOption { get; }
    public uint CalculateBlock(ReadOnlySpan<byte> input, uint start = 1);
    public uint Rotate(uint checksum, byte remove, byte add, int chunkSize);
}

public enum RollingChecksumOption
{
    Unknown = 0,
    Adler32 = 1
}