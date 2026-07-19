using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Shared.Messaging;

namespace JobBoard.Audit.Core.Data;

/// <inheritdoc cref="IAuditDataLayer"/>
/// <remarks>
/// The append and the inbox stamp commit together, exactly as every other consumer's side effect
/// (see <c>NotificationDataLayer</c>). The row key is the event id, so the primary key is a second,
/// independent backstop against a double-write — but the inbox check is the primary guard and keeps the
/// operation safe to replay under the retrying execution strategy.
/// </remarks>
public sealed class AuditDataLayer : IAuditDataLayer
{
    private readonly IAuditRepository _repository;
    private readonly IInbox _inbox;

    public AuditDataLayer(IAuditRepository repository, IInbox inbox)
    {
        _repository = repository;
        _inbox = inbox;
    }

    public Task AppendAsync(AuditEntry entry, Guid messageId, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(
            async token =>
            {
                // Idempotency: a redelivery of the same message finds its inbox row and no-ops.
                if (await _inbox.HasProcessedAsync(messageId, token))
                {
                    return;
                }

                await _repository.AddAsync(entry, token);
                await _inbox.MarkProcessedAsync(messageId, token);
            },
            cancellationToken);
}
