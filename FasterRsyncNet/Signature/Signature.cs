using System.Collections.Immutable;
using FasterRsyncNet.Chunk;

namespace FasterRsyncNet.Signature;

public record Signature
{
    public required SignatureMetadata Metadata { get; init; }
    public required ImmutableArray<ChunkSignature> Chunks { get; init; }
}