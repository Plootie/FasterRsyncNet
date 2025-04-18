using System.Collections.Immutable;

namespace FasterRsyncNet.Chunk;

public readonly record struct ChunkSignature
{
    public required ulong Offset { get; init; }
    public required ushort Length { get; init; }
    public required ImmutableArray<byte> Hash { get; init; }
    public required uint RollingChecksum { get; init; }
}