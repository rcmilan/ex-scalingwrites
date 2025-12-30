namespace ScalingWrites.Core.Data.Configurations;

public interface IShardMetadataStore
{
    Task<IReadOnlyList<ShardDescriptor>> LoadShardsAsync();
    Task ReloadShardsAsync();
    Task AddShardAsync(ShardDescriptor shard);
    Task RemoveShardAsync(string shardName);
}