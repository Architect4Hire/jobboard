using JobBoard.Notifications.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;

namespace JobBoard.Notifications.Core.Data;

/// <summary>
/// Data-only seam for the Notifications context. Extends <see cref="IRepository"/> so the data layer can
/// run the inbox check + log insert inside one transaction via
/// <see cref="IRepository.ExecuteInTransactionAsync"/>.
/// </summary>
public interface INotificationRepository : IRepository
{
    /// <summary>Stages a notification-log row for insert. The caller runs it inside a transaction.</summary>
    Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default);
}
