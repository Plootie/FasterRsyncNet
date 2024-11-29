namespace FasterRsyncNet.Hash;

public interface IRollingChecksum
{
    public RollingChecksumOption RollingChecksumOption { get; }
    public uint Append(ReadOnlySpan<byte> block);
    public void Append(byte add);
    public void Reset();
    public uint GetChecksum();
    public short WindowSize { get; init; }
}

public enum RollingChecksumOption
{
    Unknown = 0,
    Adler32 = 1
}