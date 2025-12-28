using Microsoft.EntityFrameworkCore;
using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Data;

public sealed class ShardedDbContextFactory(IShardResolver resolver) : IShardedDbContextFactory, IDbContextFactory<ShardedDbContext>
{
    public ShardedDbContext CreateDbContext(object shardKey)
    {
        var shard = resolver.Resolve(shardKey);
        return GetShardedDbContext(shard);
    }

    public ShardedDbContext GetShardedDbContext(ShardDescriptor shard)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ShardedDbContext>();
        optionsBuilder.UseMySQL(shard.ConnectionString);

        return new ShardedDbContext(optionsBuilder.Options);
    }

    // fallback if EF tooling calls factory
    public ShardedDbContext CreateDbContext() => CreateDbContext(shardKey: 0);
}
