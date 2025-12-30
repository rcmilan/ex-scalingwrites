namespace ScalingWrites.Core.Data.Configurations;

public interface IShardMigrationService
{
    Task MigrateShardAsync(string sourceShardName, string targetShardName, object shardKey);
    Task SplitShardAsync(string sourceShardName, string newShardName);
    Task MergeShardsAsync(string sourceShardName, string targetShardName);
}