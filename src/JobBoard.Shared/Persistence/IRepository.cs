namespace JobBoard.Shared.Persistence;

/// <summary>
/// The base repository seam every <c>I&lt;Feature&gt;Repository</c> extends. Its one non-query member is
/// <see cref="ExecuteInTransactionAsync{T}"/>, which runs a whole data operation — domain writes and the
/// <see cref="Messaging.IOutbox"/> row together — inside a single transaction so they commit or roll back
/// as a unit.
/// </summary>
public interface IRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> in a transaction and returns its result. The operation is passed
    /// as a callback (not a caller-opened transaction) because the Aspire Npgsql retrying execution
    /// strategy owns the transaction boundary and may replay the whole unit — so the operation must be
    /// safe to repeat.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>Void overload of <see cref="ExecuteInTransactionAsync{T}"/>.</summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
