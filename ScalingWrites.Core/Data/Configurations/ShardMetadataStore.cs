using Microsoft.Extensions.Caching.Memory;
using ScalingWrites.Core.Helpers;

namespace ScalingWrites.Core.Data.Configurations;

public sealed class ShardMetadataStore(IConfiguration config, IMemoryCache cache) : IShardMetadataStore
{
    private readonly Lock _lock = new();
    private IReadOnlyList<ShardDescriptor> _shards = [];
    private const string CacheKey = "shard_metadata";

    private async Task<IReadOnlyList<ShardDescriptor>> LoadAndUpdateShardsAsync(bool forceReload)
    {
        IReadOnlyList<ShardDescriptor> loadedShards;
        
        if (!forceReload)
        {
            // Try to get from cache first (only for LoadShardsAsync)
            if (cache.TryGetValue(CacheKey, out List<ShardDescriptor>? cachedShards) && 
                cachedShards != null && 
                cachedShards.Count > 0)
            {
                loadedShards = cachedShards.AsReadOnly();
                lock (_lock)
                {
                    if (_shards.Count == 0) // Only update if not already loaded
                    {
                        _shards = loadedShards;
                    }
                }
                return loadedShards;
            }
        }
        
        // For reload or cache miss, invalidate cache and load fresh
        if (forceReload)
        {
            ShardConfigurationHelper.InvalidateShardsCache(cache);
        }
        
        // Load from configuration using helper
        loadedShards = await ShardConfigurationHelper.LoadShardsAsync(config, cache);
        
        lock (_lock)
        {
            _shards = loadedShards;
        }

        return loadedShards;
    }

    public async Task<IReadOnlyList<ShardDescriptor>> LoadShardsAsync()
    {
        return await LoadAndUpdateShardsAsync(forceReload: false);
    }

    public async Task ReloadShardsAsync()
    {
        await LoadAndUpdateShardsAsync(forceReload: true);
    }

    public async Task AddShardAsync(ShardDescriptor shard)
    {
        IReadOnlyList<ShardDescriptor> updatedShards;
        lock (_lock)
        {
            var shards = _shards.ToList();
            if (shards.Any(s => s.Name.Equals(shard.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Shard with name '{shard.Name}' already exists.");

            shards.Add(shard);
            updatedShards = shards.AsReadOnly();
            _shards = updatedShards;
        }

        // Update cache
        cache.Set(CacheKey, updatedShards.ToList(), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        });
    }

    public async Task RemoveShardAsync(string shardName)
    {
        IReadOnlyList<ShardDescriptor> updatedShards;
        lock (_lock)
        {
            var shards = _shards.ToList();
            var shardToRemove = shards.FirstOrDefault(s => s.Name.Equals(shardName, StringComparison.OrdinalIgnoreCase));
            if (shardToRemove == null)
                throw new InvalidOperationException($"Shard with name '{shardName}' not found.");

            shards.Remove(shardToRemove);
            updatedShards = shards.AsReadOnly();
            _shards = updatedShards;
        }

        // Update cache
        cache.Set(CacheKey, updatedShards.ToList(), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        });
    }
}