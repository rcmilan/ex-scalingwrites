namespace ScalingWrites.Core.Data.Configurations;

public sealed class ShardResolver(IEnumerable<IShardResolutionStrategy> strategies) : IShardResolutionStrategy
{
    private readonly IEnumerable<IShardResolutionStrategy> _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));

    public bool CanResolve(object shardKey)
    {
        // ShardResolver can resolve any key that any strategy can resolve
        return _strategies.Any(s => s.CanResolve(shardKey));
    }

    public ShardDescriptor Resolve(object shardKey)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanResolve(shardKey));
        return strategy is null
            ? throw new InvalidOperationException($"No shard strategy registered for key type {shardKey?.GetType().Name}")
            : strategy.Resolve(shardKey);
    }
}
