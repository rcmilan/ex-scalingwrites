using System.Linq.Expressions;

namespace ScalingWrites.Core.Data.Configurations;

public interface ICrossShardQueryCoordinator
{
    Task<IEnumerable<T>> ExecuteQueryOnAllShardsAsync<T>(
        Expression<Func<ShardedDbContext, IQueryable<T>>> queryExpression) where T : class;

    Task<IEnumerable<T>> ExecuteQueryOnSpecificShardsAsync<T>(
        IEnumerable<ShardDescriptor> shards,
        Expression<Func<ShardedDbContext, IQueryable<T>>> queryExpression) where T : class;

    Task<T> AggregateResultsAsync<T>(
        IEnumerable<T> results,
        Func<IEnumerable<T>, T> aggregator);
}