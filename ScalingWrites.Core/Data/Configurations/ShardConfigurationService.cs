using Microsoft.Extensions.Caching.Memory;

namespace ScalingWrites.Core.Data.Configurations;

public sealed class ShardConfigurationService : IShardConfigurationService
{
    private readonly IConfiguration? _config;
    private readonly IMemoryCache? _cache;
    private const string CacheKey = "shard_metadata";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    // Constructor for dependency injection
    public ShardConfigurationService(IConfiguration config, IMemoryCache cache)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    // Constructor for design-time usage (no DI)
    internal ShardConfigurationService()
    {
    }

    public async Task<IReadOnlyList<ShardDescriptor>> LoadShardsAsync()
    {
        if (_config == null || _cache == null)
            throw new InvalidOperationException("Service must be initialized with IConfiguration and IMemoryCache for async operations.");

        // Try to get from cache first
        if (_cache.TryGetValue(CacheKey, out List<ShardDescriptor>? cachedShards) && 
            cachedShards != null && 
            cachedShards.Count > 0)
        {
            return cachedShards.AsReadOnly();
        }

        // Load from configuration
        var loadedShards = LoadShards(_config);

        // Cache the shards
        var shardList = loadedShards.ToList();
        _cache.Set(CacheKey, shardList, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheExpiration
        });

        return shardList.AsReadOnly();
    }

    public async Task<IReadOnlyList<ShardDescriptor>> GetCachedShardsAsync()
    {
        if (_cache == null)
            throw new InvalidOperationException("Service must be initialized with IMemoryCache for cache operations.");
        
        // Get from cache only
        if (_cache.TryGetValue(CacheKey, out List<ShardDescriptor>? cachedShards) && 
            cachedShards != null && 
            cachedShards.Count > 0)
        {
            return cachedShards.AsReadOnly();
        }
        
        throw new InvalidOperationException("Shard descriptors are not available in cache. Call LoadShardsAsync or EnsureShardsLoadedAsync first.");
    }

    public async Task<IReadOnlyList<ShardDescriptor>> TryGetCachedShardsAsync()
    {
        if (_cache == null)
            return Array.Empty<ShardDescriptor>();
        
        // Get from cache only
        if (_cache.TryGetValue(CacheKey, out List<ShardDescriptor>? cachedShards) && 
            cachedShards != null && 
            cachedShards.Count > 0)
        {
            return cachedShards.AsReadOnly();
        }
        
        return Array.Empty<ShardDescriptor>();
    }

    public async Task<IReadOnlyList<ShardDescriptor>> EnsureShardsLoadedAsync()
    {
        // This is essentially the same as LoadShardsAsync but with a clearer name
        return await LoadShardsAsync();
    }

    public bool IsCached
    {
        get
        {
            if (_cache == null)
                return false;
                
            return _cache.TryGetValue(CacheKey, out List<ShardDescriptor>? cachedShards) && 
                   cachedShards != null && 
                   cachedShards.Count > 0;
        }
    }

    public void InvalidateCache()
    {
        if (_cache == null)
            throw new InvalidOperationException("Service must be initialized with IMemoryCache for cache operations.");
        
        _cache.Remove(CacheKey);
    }

    // Static method for design-time usage
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

    // Static method for backward compatibility
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

    // Static method for backward compatibility
    public static void InvalidateShardsCache(IMemoryCache cache)
    {
        cache.Remove(CacheKey);
    }
}