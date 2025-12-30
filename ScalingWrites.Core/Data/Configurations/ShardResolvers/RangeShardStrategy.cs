namespace ScalingWrites.Core.Data.Configurations;

public sealed class RangeShardStrategy : IShardResolutionStrategy
{
    private readonly SortedDictionary<long, ShardDescriptor> _rangeMap;
    private readonly IShardConfigurationService _shardConfig;

    public RangeShardStrategy(IShardConfigurationService shardConfig)
    {
        _shardConfig = shardConfig ?? throw new ArgumentNullException(nameof(shardConfig));
        _rangeMap = BuildRangeMap(shardConfig);
    }

    public bool CanResolve(object key) => key is int or long or DateTime;

    public ShardDescriptor Resolve(object key)
    {
        if (_rangeMap.Count == 0)
            throw new InvalidOperationException("No shards configured for range-based resolution.");

        long rangeKey;
        if (key is int intKey)
            rangeKey = intKey;
        else if (key is long longKey)
            rangeKey = longKey;
        else if (key is DateTime dateKey)
            rangeKey = dateKey.Ticks;
        else
            throw new InvalidOperationException($"Unsupported key type for range resolution: {key?.GetType().Name}");

        // Find the first range that is greater than or equal to the key
        var range = _rangeMap.Keys.FirstOrDefault(r => r >= rangeKey);
        if (range == 0) // No range found, use the last one
            range = _rangeMap.Keys.Last();

        return _rangeMap[range];
    }

    private static SortedDictionary<long, ShardDescriptor> BuildRangeMap(IShardConfigurationService shardConfig)
    {
        var shards = shardConfig.EnsureShardsLoadedAsync().GetAwaiter().GetResult();
        var map = new SortedDictionary<long, ShardDescriptor>();
        var totalShards = shards.Count;

        for (int i = 0; i < totalShards; i++)
        {
            // Simple range distribution: divide the long range evenly
            var rangeStart = (long.MaxValue / totalShards) * i;
            map[rangeStart] = shards[i];
        }

        return map;
    }
}