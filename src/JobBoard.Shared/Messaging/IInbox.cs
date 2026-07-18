namespace JobBoard.Shared.Messaging;

/// <summary>
/// The idempotency ledger a consumer uses to make at-least-once delivery safe. A consumer checks
/// <see cref="HasProcessedAsync"/> and, if the message is new, applies its side effect and calls
/// <see cref="MarkProcessedAsync"/> in the same transaction, so the dedupe row and the effect commit
/// together. Both operate on the same scoped <see cref="Persistence.BaseDbContext"/>.
/// </summary>
public interface IInbox
{
    /// <summary>True if <paramref name="messageId"/> has already been handled and should be skipped.</summary>
    Task<bool> HasProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages an <c>InboxMessages</c> row recording <paramref name="messageId"/> as handled. The row is
    /// flushed and committed by the surrounding transaction, alongside the side effect.
    /// </summary>
    Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
}
