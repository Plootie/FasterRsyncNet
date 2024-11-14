namespace FasterRsyncNet.Hash;

public interface INonCryptographicHashingAlgorithm
{
    public void Append(byte[] source);
    public void Append(Stream stream);
    public void Append(ReadOnlySpan<byte> source);
    public Task AppendAsync(Stream stream, CancellationToken cancellationToken = default);
    public byte[] GetCurrentHash();
    public void GetCurrentHash(Span<byte> destination);
    public byte[] GetHashAndReset();
    public void GetHashAndReset(Span<byte> destination);
    public bool TryGetCurrentHash(Span<byte> destination, out int bytesWritten);
    public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten);
    public int HashLengthInBytes { get; }
}

public enum NonCryptographicHashingAlgorithmOption
{
    XXHash64 = 0
}