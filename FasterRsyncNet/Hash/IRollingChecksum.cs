namespace FasterRsyncNet.Hash;

public interface IRollingChecksum
{
    public RollingChecksumType RollingChecksumType { get; }
    public uint Append(ReadOnlySpan<byte> block);
    public void Append(byte add);
    public void Reset();
    public uint GetChecksum();
}

public enum RollingChecksumType
{
    Adler32 = 0,
}