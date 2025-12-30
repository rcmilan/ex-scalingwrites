namespace ScalingWrites.Core.Data.Configurations;

public sealed class ShardResolver(IReadOnlyList<ShardDescriptor> shards, IEnumerable<IShardResolutionStrategy> strategies) : IShardResolver
{
    public ShardDescriptor Resolve(object shardKey)
    {
        var strategy = strategies.FirstOrDefault(s => s.CanResolve(shardKey));
        return strategy is null
            ? throw new InvalidOperationException($"No shard strategy registered for key type {shardKey?.GetType().Name}")
            : strategy.Resolve(shardKey, shards);
    }
}
