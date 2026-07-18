using JobBoard.Notifications.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;

namespace JobBoard.Notifications.Core.Data;

/// <summary>
/// EF Core implementation of <see cref="INotificationRepository"/> over <see cref="NotificationsDbContext"/>.
/// Inherits <c>ExecuteInTransactionAsync</c> from <see cref="BaseRepository{TContext}"/>.
/// </summary>
public sealed class NotificationRepository : BaseRepository<NotificationsDbContext>, INotificationRepository
{
    public NotificationRepository(NotificationsDbContext context) : base(context)
    {
    }

    public async Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default) =>
        await Context.NotificationLogs.AddAsync(log, cancellationToken);
}
