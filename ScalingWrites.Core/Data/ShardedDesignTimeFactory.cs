using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ScalingWrites.Core.Data.Configurations;
using ScalingWrites.Core.Helpers;

namespace ScalingWrites.Core.Data;

public class ShardedDesignTimeFactory : IDesignTimeDbContextFactory<ShardedDbContext>
{
    public ShardedDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var shards = ShardConfigurationHelper.LoadShards(config);

        // default shard
        var shardName = "Shard0";

        // read arg:  Update-Database -- --shard=Shard2
        var shardArg = args?.FirstOrDefault(a => a.StartsWith("--shard=", StringComparison.OrdinalIgnoreCase));
        if (shardArg is not null)
            shardName = shardArg.Split("=", 2)[1];

        var shard = shards.Single(s => s.Name == shardName);

        var options = new DbContextOptionsBuilder<ShardedDbContext>()
            .UseMySQL(shard.ConnectionString)
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
