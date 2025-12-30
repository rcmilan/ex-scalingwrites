using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ScalingWrites.Core.Data.Configurations;

public sealed class TransactionCoordinator(IShardDbContextFactory contextFactory) : ITransactionCoordinator
{
    public async Task ExecuteInTransactionAsync(Func<Task> operation, IEnumerable<ShardDescriptor> shards)
    {
        var shardContexts = new List<(ShardDescriptor Shard, ShardedDbContext Context, IDbContextTransaction Transaction)>();

        try
        {
            // Prepare phase: Create transactions on all shards
            foreach (var shard in shards)
            {
                var context = await contextFactory.CreateScopedDbContextAsync(shard.Id);
                var transaction = await context.Database.BeginTransactionAsync();
                shardContexts.Add((shard, context, transaction));
            }

            // Execute the operation
            await operation();

            // Commit phase: Commit all transactions
            foreach (var (_, _, transaction) in shardContexts)
            {
                await transaction.CommitAsync();
            }
        }
        catch (Exception)
        {
            // Rollback phase: Rollback all transactions
            foreach (var (_, _, transaction) in shardContexts)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Log rollback failure but continue with other rollbacks
                }
            }
            throw;
        }
        finally
        {
            // Cleanup: Dispose contexts and transactions
            foreach (var (_, context, transaction) in shardContexts)
            {
                await transaction.DisposeAsync();
                await context.DisposeAsync();
            }
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, IEnumerable<ShardDescriptor> shards)
    {
        T result = default!;
        await ExecuteInTransactionAsync(async () => { result = await operation(); }, shards);
        return result;
    }

    public async Task ExecuteTwoPhaseCommitAsync(Func<Task> prepareOperation, Func<Task> commitOperation, IEnumerable<ShardDescriptor> shards)
    {
        var shardContexts = new List<(ShardDescriptor Shard, ShardedDbContext Context, IDbContextTransaction Transaction)>();

        try
        {
            // Phase 1: Prepare - Create transactions and execute prepare operations
            foreach (var shard in shards)
            {
                var context = await contextFactory.CreateScopedDbContextAsync(shard.Id);
                var transaction = await context.Database.BeginTransactionAsync();
                shardContexts.Add((shard, context, transaction));
            }

            await prepareOperation();

            // Phase 2: Commit - Execute commit operations and commit transactions
            await commitOperation();

            foreach (var (_, _, transaction) in shardContexts)
            {
                await transaction.CommitAsync();
            }
        }
        catch (Exception)
        {
            // Rollback all transactions
            foreach (var (_, _, transaction) in shardContexts)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Log rollback failure
                }
            }
            throw;
        }
        finally
        {
            // Cleanup
            foreach (var (_, context, transaction) in shardContexts)
            {
                await transaction.DisposeAsync();
                await context.DisposeAsync();
            }
        }
    }
}