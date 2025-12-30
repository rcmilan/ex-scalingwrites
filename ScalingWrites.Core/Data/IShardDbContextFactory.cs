using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Data;

public interface IShardDbContextFactory
{
    Task<ShardDescriptor> ResolveShardAsync(object shardKey);
    Task<string> GetConnectionStringAsync(ShardDescriptor shard);
    Task<ShardedDbContext> CreateScopedDbContextAsync(object shardKey);
}