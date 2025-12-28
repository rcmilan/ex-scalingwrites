namespace ScalingWrites.Core.Data;

public interface IShardedDbContextFactory
{
    ShardedDbContext CreateDbContext(object shardKey);
}
