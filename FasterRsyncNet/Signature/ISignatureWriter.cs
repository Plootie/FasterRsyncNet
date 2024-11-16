using FasterRsyncNet.Chunk;

namespace FasterRsyncNet.Signature;

public interface ISignatureWriter : IDisposable, IAsyncDisposable
{
    public Stream BaseStream { get; }
    public void WriteMetadata(SignatureMetadata metadata);
    public Task WriteMetadataAsync(SignatureMetadata metadata);
    public void WriteChunk(ChunkSignature signature);
    public Task WriteChunkAsync(ChunkSignature signature);
}