using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Helpers;

public static class ShardConfigurationHelper
{
    public static IReadOnlyList<ShardDescriptor> LoadShards(IConfiguration config)
    {
        var shards = config
            .GetSection("ConnectionStrings")
            .GetChildren()
            .Where(c => c.Key.StartsWith("Shard", StringComparison.OrdinalIgnoreCase))
            .Select((c, index) => new ShardDescriptor(
                index,
                c.Key,
                c.Value ?? throw new InvalidOperationException(
                    $"Missing connection string for {c.Key}")
            ))
            .ToList();

        if (shards.Count == 0)
            throw new InvalidOperationException("No shard connection strings were found.");

        return shards.AsReadOnly();
    }
}
