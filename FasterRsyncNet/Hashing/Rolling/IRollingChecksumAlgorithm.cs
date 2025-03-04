namespace FasterRsyncNet.Hashing.Rolling;

public interface IRollingChecksumAlgorithm
{
    public string AlgorithmIdentifier { get; }
    public uint CalculateBlock(ReadOnlySpan<byte> source, uint start = 1);
    public uint Rotate(uint checksum, byte remove, byte add, int chunkSize);
}