namespace ScalingWrites.Core.Data.Configurations;

public sealed class GuidHashShardStrategy(IReadOnlyList<ShardDescriptor> shards) : IShardResolutionStrategy
{
    private readonly ConsistentHashRing _ring = new(shards);

    public bool CanResolve(object key) => key is Guid;

    public ShardDescriptor Resolve(object key, IReadOnlyList<ShardDescriptor> _)
        => _ring.Resolve(key.ToString()!);
}
