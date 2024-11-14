using System.Collections.Immutable;
using System.IO.Hashing;
using FasterRsyncNet.Hash.HashingAlgorithms.NonCryptographic;
using FasterRsyncNet.Hash.HashingAlgorithms.Rolling;

namespace FasterRsyncNet.Hash;

//There must be a better way to achieve this.
//TODO: Revisit this system. This feels very inflexible
public static class HashHelper
{
    public static readonly ImmutableDictionary<RollingChecksumOption, Type> RollingChecksumMapper =
        new Dictionary<RollingChecksumOption, Type>
        {
            { RollingChecksumOption.Adler32, typeof(Adler32) }
        }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<NonCryptographicHashingAlgorithmOption, Type>
        NonCryptographicHashingAlgorithmMapper = new Dictionary<NonCryptographicHashingAlgorithmOption, Type>
        {
            { NonCryptographicHashingAlgorithmOption.XXHash64, typeof(XXHash64) }
        }.ToImmutableDictionary();
}