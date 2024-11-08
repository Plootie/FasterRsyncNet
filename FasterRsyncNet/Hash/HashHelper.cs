using System.Collections.Immutable;
using System.IO.Hashing;
using FasterRsyncNet.Hash.HashingAlgorithms.Rolling;

namespace FasterRsyncNet.Hash;

//There must be a better way to achieve this.
//TODO: Revisit this system. This feels very inflexible
public static class HashHelper
{
    public static readonly ImmutableDictionary<RollingChecksumType, Type> RollingChecksumMapper =
        new Dictionary<RollingChecksumType, Type>
        {
            { RollingChecksumType.Adler32, typeof(Adler32) }
        }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<NonCryptographicHashingAlgorithmType, Type>
        NonCryptographicHashingAlgorithmMapper = new Dictionary<NonCryptographicHashingAlgorithmType, Type>
        {
            { NonCryptographicHashingAlgorithmType.XXHash64, typeof(XxHash64) }
        }.ToImmutableDictionary();
}