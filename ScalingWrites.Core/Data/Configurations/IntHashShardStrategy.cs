namespace ScalingWrites.Core.Data.Configurations;

public sealed class IntHashShardStrategy(IEnumerable<ShardDescriptor> shards) : IShardResolutionStrategy
{
    private readonly ConsistentHashRing _ring = new(shards);

    public bool CanResolve(object key) => key is int;

    public ShardDescriptor Resolve(object key, IReadOnlyList<ShardDescriptor> _)
        => _ring.Resolve(key.ToString()!);
}
