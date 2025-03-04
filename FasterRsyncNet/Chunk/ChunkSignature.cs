namespace FasterRsyncNet.Chunk;

public readonly record struct ChunkSignature
{
    public required ulong Offset { get; init; }
    public required ushort Length { get; init; }
    public required byte[] Hash { get; init; }
    public required uint RollingChecksum { get; init; }
}