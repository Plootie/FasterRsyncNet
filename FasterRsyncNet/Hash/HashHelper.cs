using System.Collections.Immutable;
using FasterRsyncNet.Hash.HashingAlgorithms.NonCryptographic;
using FasterRsyncNet.Hash.HashingAlgorithms.Rolling;

namespace FasterRsyncNet.Hash;

//There must be a better way to achieve this.
//TODO: Revisit this system. This feels very inflexible
public static class HashHelper
{
    public static readonly ImmutableDictionary<NonCryptographicHashingAlgorithmOption, Type>
        NonCryptographicHashingAlgorithmMapper = new Dictionary<NonCryptographicHashingAlgorithmOption, Type>
        {
            { NonCryptographicHashingAlgorithmOption.XXHash64, typeof(XXHash64) }
        }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<RollingChecksumOption, Type>
        RollingChecksumMapper = new Dictionary<RollingChecksumOption, Type>
        {
            { RollingChecksumOption.Adler32, typeof(Adler32) }
        }.ToImmutableDictionary();
    
    //TODO: This is horrifyingly slow for what it does. Look at a better way of creating these classes!
    public static T InstanceFromType<T>(Type type, object[]? args = null)
    {
        return (T)(Activator.CreateInstance(type, args:args) ?? throw new InvalidOperationException($"Could not create an instance of type {type.FullName}."));
    }
}