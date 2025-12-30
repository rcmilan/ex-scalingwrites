namespace ScalingWrites.Core.Data.Configurations;

public interface ITransactionCoordinator
{
    Task ExecuteInTransactionAsync(Func<Task> operation, IEnumerable<ShardDescriptor> shards);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, IEnumerable<ShardDescriptor> shards);
    Task ExecuteTwoPhaseCommitAsync(Func<Task> prepareOperation, Func<Task> commitOperation, IEnumerable<ShardDescriptor> shards);
}