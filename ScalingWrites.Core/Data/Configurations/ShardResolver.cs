namespace ScalingWrites.Core.Data.Configurations;

public sealed class ShardResolver(IConfiguration config, IEnumerable<IShardResolutionStrategy> strategies) : IShardResolver
{
    private readonly IReadOnlyList<ShardDescriptor> _shards = config.GetSection("ShardMap:Shards")
            .Get<List<ShardDescriptor>>()
            ?? throw new InvalidOperationException("Shard map not configured.");

    private readonly IReadOnlyList<IShardResolutionStrategy> _strategies = [.. strategies];

    public ShardDescriptor Resolve(object shardKey)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanResolve(shardKey));
        return strategy is null
            ? throw new InvalidOperationException($"No shard strategy registered for key type {shardKey?.GetType().Name}")
            : strategy.Resolve(shardKey, _shards);
    }
}
