namespace ScalingWrites.Core.Data.Configurations;

public sealed class IntHashShardStrategy : IShardResolutionStrategy
{
    private readonly ConsistentHashRing _ring;
    private readonly IShardConfigurationService _shardConfig;

    public IntHashShardStrategy(IShardConfigurationService shardConfig)
    {
        _shardConfig = shardConfig ?? throw new ArgumentNullException(nameof(shardConfig));
        _ring = new ConsistentHashRing(shardConfig.EnsureShardsLoadedAsync().GetAwaiter().GetResult());
    }

    public bool CanResolve(object key) => key is int;

    public ShardDescriptor Resolve(object key)
        => _ring.Resolve(key.ToString()!);
}
