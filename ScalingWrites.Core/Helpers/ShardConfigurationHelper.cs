using Microsoft.Extensions.Caching.Memory;
using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Helpers;

public static class ShardConfigurationHelper
{
    private const string CacheKey = "shard_metadata";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    public static IReadOnlyList<ShardDescriptor> LoadShards(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        
        var connectionStringsSection = config.GetSection("ConnectionStrings");
        if (!connectionStringsSection.Exists())
            throw new InvalidOperationException("ConnectionStrings section not found in configuration.");
        
        var shardConfigs = connectionStringsSection
            .GetChildren()
            .Where(c => c.Key.StartsWith("Shard", StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        if (shardConfigs.Count == 0)
            throw new InvalidOperationException("No shard connection strings were found. Expected keys starting with 'Shard' in ConnectionStrings section.");

        var shards = shardConfigs
            .Select((c, index) => new ShardDescriptor(
                index,
                c.Key,
                c.Value ?? throw new InvalidOperationException(
                    $"Missing connection string for {c.Key}")
            ))
            .ToList();

        return shards.AsReadOnly();
    }

    public static async Task<IReadOnlyList<ShardDescriptor>> LoadShardsAsync(IConfiguration config, IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(cache);
        
        // Try to get from cache first
        if (cache.TryGetValue(CacheKey, out List<ShardDescriptor>? cachedShards) && 
            cachedShards != null && 
            cachedShards.Count > 0)
        {
            return cachedShards.AsReadOnly();
        }

        // Load from configuration
        var loadedShards = LoadShards(config);

        // Cache the shards
        var shardList = loadedShards.ToList();
        cache.Set(CacheKey, shardList, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheExpiration
        });

        return shardList.AsReadOnly();
    }

    public static void InvalidateShardsCache(IMemoryCache cache)
    {
        cache.Remove(CacheKey);
    }
}
