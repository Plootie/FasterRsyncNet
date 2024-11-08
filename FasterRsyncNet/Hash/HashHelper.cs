using System.IO.Hashing;
using FasterRsyncNet.Hash.HashingAlgorithms.Rolling;

namespace FasterRsyncNet.Hash;

//There must be a better way to achieve this.
//TODO: Revisit this system. This feels very inflexible
public static class HashHelper
{
    private static Dictionary<RollingChecksumType, Type> RollingChecksumMapper = new()
    {
        { RollingChecksumType.Adler32, typeof(Adler32) }
    };

    private static Dictionary<NonCryptographicHashingAlgorithmType, Type> NonCryptographicHashingAlgorithmMapper = new()
    {
        { NonCryptographicHashingAlgorithmType.XXHash64, typeof(XxHash64) },
    };
}