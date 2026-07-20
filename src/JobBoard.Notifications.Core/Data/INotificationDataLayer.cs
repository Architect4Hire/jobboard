using JobBoard.Notifications.Core.Managers.Models.Domain;

namespace JobBoard.Notifications.Core.Data;

/// <summary>
/// Records a notification idempotently. Depends on <see cref="INotificationRepository"/> and the shared
/// <c>IInbox</c>; holds no <c>DbContext</c> and no <c>IOutbox</c> (Notifications publishes nothing).
/// </summary>
public interface INotificationDataLayer
{
    /// <summary>
    /// Writes <paramref name="log"/> and records <paramref name="messageId"/> in the inbox, in one
    /// transaction — so a redelivery of the same message (same id) finds its inbox row and no-ops,
    /// writing no duplicate notification.
    /// </summary>
    Task RecordAsync(NotificationLog log, Guid messageId, CancellationToken cancellationToken = default);
}
