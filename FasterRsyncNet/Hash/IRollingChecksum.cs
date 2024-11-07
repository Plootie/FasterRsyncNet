namespace FasterRsyncNet.Hash;

public interface IRollingChecksum
{
    RollingChecksumType RollingChecksumType { get; }
    uint Append(ReadOnlySpan<byte> block);
    void Append(byte add);
    void Reset();
    uint GetChecksum();
}

public enum RollingChecksumType
{
    Adler32 = 0,
}