namespace ScalingWrites.Core.Data.Configurations;

public interface IShardConfigurationService
{
    // Loads shards from cache or configuration (if not cached)
    Task<IReadOnlyList<ShardDescriptor>> LoadShardsAsync();
    
    // Gets cached shards only (throws if not in cache)
    Task<IReadOnlyList<ShardDescriptor>> GetCachedShardsAsync();
    
    // Gets cached shards or empty list if not available
    Task<IReadOnlyList<ShardDescriptor>> TryGetCachedShardsAsync();
    
    // Ensures shards are loaded (loads from config if not cached)
    Task<IReadOnlyList<ShardDescriptor>> EnsureShardsLoadedAsync();
    
    // Invalidates the cache
    void InvalidateCache();
    
    // Checks if shards are currently cached
    bool IsCached { get; }
}