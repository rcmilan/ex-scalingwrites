using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ScalingWrites.Core.Data.Configurations;

public sealed class CrossShardQueryCoordinator(IShardConfigurationService configurationService, IShardDbContextFactory contextFactory) : ICrossShardQueryCoordinator
{
    public async Task<IEnumerable<T>> ExecuteQueryOnAllShardsAsync<T>(
        Expression<Func<ShardedDbContext, IQueryable<T>>> queryExpression) where T : class
    {
        var shards = await configurationService.LoadShardsAsync();
        return await ExecuteQueryOnSpecificShardsAsync(shards, queryExpression);
    }

    public async Task<IEnumerable<T>> ExecuteQueryOnSpecificShardsAsync<T>(
        IEnumerable<ShardDescriptor> shards,
        Expression<Func<ShardedDbContext, IQueryable<T>>> queryExpression) where T : class
    {
        var tasks = shards.Select(async shard =>
        {
            await using var db = await contextFactory.CreateScopedDbContextAsync(shard.Id);
            var query = queryExpression.Compile()(db);
            return await query.ToListAsync();
        });

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r);
    }

    public Task<T> AggregateResultsAsync<T>(
        IEnumerable<T> results,
        Func<IEnumerable<T>, T> aggregator)
    {
        return Task.FromResult(aggregator(results));
    }
}