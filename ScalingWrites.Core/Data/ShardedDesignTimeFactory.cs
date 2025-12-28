using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ScalingWrites.Core.Data.Configurations;
using ScalingWrites.Core.Helpers;

namespace ScalingWrites.Core.Data;

public class ShardedDesignTimeFactory : IDesignTimeDbContextFactory<ShardedDbContext>
{
    public ShardedDbContext CreateDbContext(string[] args)
    {
        // Load config the same way your app would
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var shards = ShardConfigurationHelper.LoadShards(config);

        // pick canonical shard
        var shard0 = shards[0];

        var options = new DbContextOptionsBuilder<ShardedDbContext>()
            .UseMySQL(shard0.ConnectionString)
            .Options;

        return new ShardedDbContext(options);
    }

    private sealed class StaticShardResolver(IEnumerable<ShardDescriptor> shards) : IShardResolver
    {
        private readonly Dictionary<object, ShardDescriptor> _byId = shards.ToDictionary(s => (object)s.Id);

        public ShardDescriptor Resolve(object shardKey)
        {
            return _byId.TryGetValue(shardKey, out var shard)
                ? shard
                : throw new InvalidOperationException($"Unknown shard key: {shardKey}");
        }
    }
}
