using System.Collections.Immutable;
using FasterRsyncNet.Chunk;

namespace FasterRsyncNet.Signature;

public class Signature(SignatureMetadata metadata, ImmutableArray<ChunkSignature> chunks)
{
    public SignatureMetadata Metadata { get; init; } = metadata;
    public ImmutableArray<ChunkSignature> Chunks { get; init; } = chunks;
}