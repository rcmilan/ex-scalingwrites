namespace ScalingWrites.Core.Data.Configurations;

public interface IShardResolver
{
    ShardDescriptor Resolve(object shardKey);
}
