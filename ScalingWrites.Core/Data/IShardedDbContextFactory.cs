using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Data;

public interface IShardedDbContextFactory
{
    ShardedDbContext CreateDbContext(object shardKey);
    ShardedDbContext GetShardedDbContext(ShardDescriptor shard);
}
