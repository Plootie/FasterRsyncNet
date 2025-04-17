using FasterRsyncNet.Chunk;

namespace FasterRsyncNet.Signature;

public interface ISignatureWriter : IDisposable, IAsyncDisposable
{
    public Stream BaseStream { get; }
    public void WriteHeader();
    public void WriteMetadata(SignatureMetadata metadata);
    public void WriteChunk(ReadOnlySpan<byte> hash, uint checksum);
    public void WriteFinalChunkLength(ushort length);
}