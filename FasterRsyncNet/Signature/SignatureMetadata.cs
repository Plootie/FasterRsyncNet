namespace FasterRsyncNet.Signature;

public readonly record struct SignatureMetadata
{
    public required byte Version { get; init; }
    public required ushort ChunkSize { get; init; }
    public required string HashAlgorithmIdentifier { get; init; }
    public required string RollingHashAlgorithmIdentifier { get; init; }
}