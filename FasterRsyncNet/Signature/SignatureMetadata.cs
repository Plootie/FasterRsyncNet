using System.ComponentModel;
using FasterRsyncNet.Hash;

namespace FasterRsyncNet.Signature;

public struct SignatureMetadata
{
    public const byte SignatureMetadataVersion = 1;
    public const int SignatureMetadataSize = 3;

    public readonly byte Version;
    public readonly byte[] Hash;
    public readonly IRollingChecksum RollingChecksumInstance;
    public readonly INonCryptographicHashingAlgorithm NonCryptographicHashingAlgorithmInstance;

    public SignatureMetadata(byte[] hash, NonCryptographicHashingAlgorithmType hashingType,
        RollingChecksumType rollingChecksumType, byte version = SignatureMetadataVersion)
    {
        Hash = hash;
        Version = version;

        Type nonCryptographicHashingAsType = HashHelper.NonCryptographicHashingAlgorithmMapper[hashingType];
        //Probably the wrong exception type here?
        RollingChecksumInstance = (IRollingChecksum)(Activator.CreateInstance(nonCryptographicHashingAsType) ??
                                                     throw new InvalidEnumArgumentException());

        Type rollingChecksumAsType = HashHelper.RollingChecksumMapper[rollingChecksumType];
        NonCryptographicHashingAlgorithmInstance =
            (INonCryptographicHashingAlgorithm)(Activator.CreateInstance(rollingChecksumAsType) ??
                                                throw new InvalidEnumArgumentException());
    }
}