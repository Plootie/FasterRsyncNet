namespace FasterRsyncNet.Core;

public class ByteArrayComparer : IEqualityComparer<byte[]>
{
    private readonly Dictionary<object, int> _hashCodeCache = new();
    
    public bool Equals(byte[]? x, byte[]? y)
    {
        if (x == null || y == null) return x == y;
        return x.SequenceEqual(y);
    }

    public int GetHashCode(byte[] obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        
        bool cached = _hashCodeCache.TryGetValue(obj, out int hashCode);
        if(cached) return hashCode;
        
        unchecked
        {
            int hash = 17;
            foreach (byte b in obj)
            {
                hash = hash * 31 + b;
            }
            _hashCodeCache[obj] = hash;
            return hash;
        }
    }
}