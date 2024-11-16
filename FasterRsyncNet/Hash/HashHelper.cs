using System.Collections.Immutable;
using FasterRsyncNet.Hash.HashingAlgorithms.NonCryptographic;

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
    
    public static T InstanceFromType<T>(Type type)
    {
        return (T)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Could not create an instance of type {type.FullName}."));
    }
}