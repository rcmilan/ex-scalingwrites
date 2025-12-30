using Microsoft.EntityFrameworkCore;
using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Data;

public sealed class ShardDbContextFactory(IEnumerable<IShardResolutionStrategy> strategies) : IShardDbContextFactory, IDbContextFactory<ShardedDbContext>
{
    private readonly IEnumerable<IShardResolutionStrategy> _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));

    public async Task<ShardDescriptor> ResolveShardAsync(object shardKey)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanResolve(shardKey));
        return strategy is null
            ? throw new InvalidOperationException($"No shard strategy registered for key type {shardKey?.GetType().Name}")
            : strategy.Resolve(shardKey);
    }

    public async Task<ShardedDbContext> CreateScopedDbContextAsync(object shardKey)
    {
        var shard = await ResolveShardAsync(shardKey);

        var optionsBuilder = new DbContextOptionsBuilder<ShardedDbContext>();
        optionsBuilder.UseMySQL(shard.ConnectionString);

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