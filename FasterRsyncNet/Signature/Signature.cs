using System.Collections.Immutable;
using System.IO.Hashing;
using FasterRsyncNet.Chunk;
using FasterRsyncNet.Hash;

namespace FasterRsyncNet.Signature;

public class Signature
{
    private readonly Lazy<INonCryptographicHashingAlgorithm> _hashAlgorithm;
    private readonly Lazy<IRollingChecksum> _rollingChecksum;
    public Signature(SignatureMetadata metadata)
    {
        Chunks = [];
        Metadata = metadata;

        //TODO: Look at making this static somehow. Feels wrong creating a bunch of duplicate Func all doing the same thing
        _hashAlgorithm = new Lazy<INonCryptographicHashingAlgorithm>(() =>
            InstanceFromType<INonCryptographicHashingAlgorithm>(
                HashHelper.NonCryptographicHashingAlgorithmMapper[HashAlgorithmOption]));

        _rollingChecksum = new Lazy<IRollingChecksum>(() =>
            InstanceFromType<IRollingChecksum>(HashHelper.RollingChecksumMapper[RollingChecksumOption]));
    }

    //TODO: Move this somewhere else. This is needed by more than just this class
    private static T InstanceFromType<T>(Type type)
    {
        return (T)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Could not create an instance of type {type.FullName}."));
    }

    public NonCryptographicHashingAlgorithmOption HashAlgorithmOption => Metadata.NonCryptographicHashingAlgorithmOption;
    public RollingChecksumOption RollingChecksumOption => Metadata.RollingChecksumOption;
    public INonCryptographicHashingAlgorithm HashAlgorithm => _hashAlgorithm.Value;
    public IRollingChecksum RollingChecksum => _rollingChecksum.Value;
    public ImmutableArray<ChunkSignature> Chunks { get; internal set; }
    public SignatureMetadata Metadata { get; }
}