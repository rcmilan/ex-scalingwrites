namespace ScalingWrites.Core.Data.Configurations;

public sealed class GuidHashShardStrategy : IShardResolutionStrategy
{
    private readonly ConsistentHashRing _ring;
    private readonly IShardConfigurationService _shardConfig;

    public GuidHashShardStrategy(IShardConfigurationService shardConfig)
    {
        _shardConfig = shardConfig ?? throw new ArgumentNullException(nameof(shardConfig));
        _ring = new ConsistentHashRing(shardConfig.EnsureShardsLoadedAsync().GetAwaiter().GetResult());
    }

    public bool CanResolve(object key) => key is Guid;

    public ShardDescriptor Resolve(object key)
        => _ring.Resolve(key.ToString()!);
}
