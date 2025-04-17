using FasterRsyncNet.Chunk;
using FasterRsyncNet.Core;

namespace FasterRsyncNet.Signature;

public class SignatureWriter(Stream outputStream) : ISignatureWriter
{
    public Stream BaseStream => _bw.BaseStream;
    private readonly BinaryWriter _bw = new(outputStream);

    public void WriteHeader()
    {
        _bw.Write(BinaryFormat.SignatureHeader.AsSpan());
    }

    public void WriteMetadata(SignatureMetadata metadata)
    {
        //TODO: Merge this into a single write
        _bw.Write(metadata.Version);
        _bw.Write(metadata.ChunkSize);
        _bw.Write(metadata.ChunkCount);
        _bw.Write(metadata.HashLength);
        _bw.Write(metadata.HashAlgorithmIdentifier);
        _bw.Write(metadata.RollingHashAlgorithmIdentifier);
    }

    public void WriteChunk(ReadOnlySpan<byte> hash, uint checksum)
    {
        _bw.Write(hash);
        _bw.Write(checksum);
    }

    public void WriteFinalChunkLength(ushort length)
    {
        _bw.Write(length);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _bw.Flush();
        _bw.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Task.Run(() => _bw.Dispose());
        await _bw.DisposeAsync();
    }
}