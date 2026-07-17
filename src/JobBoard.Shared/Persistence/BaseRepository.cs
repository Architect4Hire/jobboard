using Microsoft.EntityFrameworkCore;

namespace JobBoard.Shared.Persistence;

/// <summary>
/// Base class for every service's repositories. It exposes the typed <typeparamref name="TContext"/> for
/// queries and implements <see cref="ExecuteInTransactionAsync{T}"/> once for all of them.
/// </summary>
/// <remarks>
/// The callback form is mandatory, not stylistic: the Aspire Npgsql integration turns on
/// retry-on-failure, and its execution strategy refuses to run inside a transaction it did not open
/// ("does not support user-initiated transactions"). Handing the whole operation in lets the strategy
/// own the boundary and replay the unit on a transient fault. Two consequences the callers must honour:
/// the operation may run more than once (so it must be safe to repeat — the outbox keys on a
/// deterministic event id for exactly this reason), and only work done through this context is
/// transactional.
/// </remarks>
public abstract class BaseRepository<TContext> : IRepository
    where TContext : BaseDbContext
{
    protected BaseRepository(TContext context) => Context = context;

    protected TContext Context { get; }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = Context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await Context.Database.BeginTransactionAsync(token);

            var result = await operation(token);

            // Flush anything the operation staged (domain rows AND the outbox row) inside the transaction,
            // then commit them together. A throw on any leg skips the commit and disposes the transaction,
            // rolling every staged change back.
            await Context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);

            return result;
        }, cancellationToken);
    }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync<object?>(async token =>
        {
            await operation(token);
            return null;
        }, cancellationToken);
}
