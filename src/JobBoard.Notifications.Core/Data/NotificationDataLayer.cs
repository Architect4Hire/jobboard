using JobBoard.Notifications.Core.Managers.Models.Domain;
using JobBoard.Shared.Messaging;

namespace JobBoard.Notifications.Core.Data;

/// <inheritdoc cref="INotificationDataLayer"/>
public sealed class NotificationDataLayer : INotificationDataLayer
{
    private readonly INotificationRepository _repository;
    private readonly IInbox _inbox;

    public NotificationDataLayer(INotificationRepository repository, IInbox inbox)
    {
        _repository = repository;
        _inbox = inbox;
    }

    public Task RecordAsync(NotificationLog log, Guid messageId, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(
            async token =>
            {
                // Idempotency: a redelivery of the same message finds its inbox row and no-ops.
                if (await _inbox.HasProcessedAsync(messageId, token))
                {
                    return;
                }

                await _repository.AddAsync(log, token);
                await _inbox.MarkProcessedAsync(messageId, token);
            },
            cancellationToken);
}
