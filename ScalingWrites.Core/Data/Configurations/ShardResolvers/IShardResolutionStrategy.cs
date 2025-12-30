namespace ScalingWrites.Core.Data.Configurations;

public interface IShardResolutionStrategy
{
    bool CanResolve(object shardKey);
    ShardDescriptor Resolve(object shardKey);
}
