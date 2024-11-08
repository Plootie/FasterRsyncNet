using System.IO.Hashing;

namespace FasterRsyncNet.Hash.HashingAlgorithms.NonCryptographic;

//TODO: This feels... wrong to do. Look at revising this in the future
public class XXHash64 : INonCryptographicHashingAlgorithm
{
    private readonly XxHash64 _xxHash64 = new();

    public void Append(byte[] source)
    {
        _xxHash64.Append(source);
    }

    public void Append(Stream stream)
    {
        _xxHash64.Append(stream);
    }

    public void Append(ReadOnlySpan<byte> source)
    {
        _xxHash64.Append(source);
    }

    public Task AppendAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return _xxHash64.AppendAsync(stream, cancellationToken);
    }

    public byte[] GetCurrentHash()
    {
        return _xxHash64.GetCurrentHash();
    }

    public void GetCurrentHash(Span<byte> destination)
    {
        _xxHash64.GetCurrentHash(destination);
    }

    public byte[] GetHashAndReset()
    {
        return _xxHash64.GetHashAndReset();
    }

    public void GetHashAndReset(Span<byte> destination)
    {
        _xxHash64.GetHashAndReset(destination);
    }

    public bool TryGetCurrentHash(Span<byte> destination, out int bytesWritten)
    {
        return _xxHash64.TryGetCurrentHash(destination, out bytesWritten);
    }

    public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
    {
        return _xxHash64.TryGetHashAndReset(destination, out bytesWritten);
    }
}