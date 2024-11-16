using System.ComponentModel;
using FasterRsyncNet.Core;
using FasterRsyncNet.Hash;

namespace FasterRsyncNet.Signature;

//TODO: Rework this a little. It's pretty clunky to use right now
public struct SignatureMetadata(
    byte[] hash,
    NonCryptographicHashingAlgorithmOption hashingOption,
    byte version = SignatureMetadata.SignatureMetadataVersion)
{
    public const byte SignatureMetadataVersion = 1;
    /// <summary>
    /// Size of Metadata on disk (Does not include hash size which is variable by implementation)
    /// </summary>
    public static int SignatureMetadataSize => FasterRsyncBinaryFormat.SignatureHeader.Length + 2;

    public readonly byte Version = version;
    public readonly byte[] Hash = hash;
    public readonly NonCryptographicHashingAlgorithmOption NonCryptographicHashingAlgorithmOption = hashingOption;
}