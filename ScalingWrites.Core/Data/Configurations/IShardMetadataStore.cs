namespace ScalingWrites.Core.Data.Configurations;

public interface IShardMetadataStore
{
    Task<IReadOnlyList<ShardDescriptor>> LoadShardsAsync();
    Task<ShardDescriptor> GetShardAsync(object shardKey);
    Task ReloadShardsAsync();
    Task AddShardAsync(ShardDescriptor shard);
    Task RemoveShardAsync(string shardName);
}