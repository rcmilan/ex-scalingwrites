using System.Security.Cryptography;
using System.Text;

namespace ScalingWrites.Core.Data.Configurations;

public sealed class ConsistentHashRing
{
    private readonly SortedDictionary<int, ShardDescriptor> _ring = [];

    public ConsistentHashRing(IEnumerable<ShardDescriptor> shards, int replicas = 100)
    {
        if (_ring.Count > 0) return;

        foreach (var shard in shards)
        {
            for (int i = 0; i < replicas; i++)
            {
                var key = Hash($"{shard.Name}:{i}");
                _ring[key] = shard;
            }
        }
    }

    public ShardDescriptor Resolve(string key)
    {
        var node = _ring.Keys.FirstOrDefault(k => k >= Hash(key));
        if (node == 0) node = _ring.Keys.First();
        return _ring[node];
    }

    private static int Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(bytes, 0);
    }
}
