using FasterRsyncNet.Chunk;

namespace FasterRsyncNet.Signature;

public interface ISignatureWriter
{
    public interface ISignatureWriter
    {
        Stream BaseStream { get; }
        void WriteMetadata(SignatureMetadata metadata);
        Task WriteMetadataAsync(SignatureMetadata metadata);
        void WriteChunk(ChunkSignature signature);
        Task WriteChunkAsync(ChunkSignature signature);
    }
}