using Microsoft.EntityFrameworkCore;
using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Data;

public sealed class ShardDbContextFactory(IShardResolver resolver) : IShardDbContextFactory, IDbContextFactory<ShardedDbContext>
{
    public async Task<ShardDescriptor> ResolveShardAsync(object shardKey)
    {
        return resolver.Resolve(shardKey);
    }

    public Task<string> GetConnectionStringAsync(ShardDescriptor shard)
    {
        return Task.FromResult(shard.ConnectionString);
    }

    public async Task<ShardedDbContext> CreateScopedDbContextAsync(object shardKey)
    {
        var shard = await ResolveShardAsync(shardKey);
        var connectionString = await GetConnectionStringAsync(shard);

        var optionsBuilder = new DbContextOptionsBuilder<ShardedDbContext>();
        optionsBuilder.UseMySQL(connectionString);

        return new ShardedDbContext(optionsBuilder.Options);
    }

    public ShardedDbContext CreateDbContext()
    {
        // This is a fallback for EF Core tooling - should not be used in application code
        var optionsBuilder = new DbContextOptionsBuilder<ShardedDbContext>();
        optionsBuilder.UseMySQL("server=localhost;user=root;password=rootpass;database=scalingwrites_default;");
        return new ShardedDbContext(optionsBuilder.Options);
    }
}