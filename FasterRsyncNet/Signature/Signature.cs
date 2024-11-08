using System.Collections.Immutable;
using System.IO.Hashing;
using FasterRsyncNet.Chunk;
using FasterRsyncNet.Hash;

namespace FasterRsyncNet.Signature;

public class Signature
{
    public Signature(SignatureMetadata metadata)
    {
        Chunks = [];
        Metadata = metadata;
        HashAlgorithm = Metadata.NonCryptographicHashingAlgorithmInstance;
        RollingChecksum = Metadata.RollingChecksumInstance;
    }

    public INonCryptographicHashingAlgorithm HashAlgorithm { get; }
    public IRollingChecksum RollingChecksum { get; }
    public ImmutableList<ChunkSignature> Chunks { get; internal set; }
    public SignatureMetadata Metadata { get; }
}