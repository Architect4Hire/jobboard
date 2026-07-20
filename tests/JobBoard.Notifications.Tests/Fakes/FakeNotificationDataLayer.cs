using JobBoard.Notifications.Core.Data;
using JobBoard.Notifications.Core.Managers.Models.Domain;

namespace JobBoard.Notifications.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="INotificationDataLayer"/> for business tests. Captures the log business built
/// and the message id it keyed on, so a test can assert the event→log mapping and that the dedupe key is
/// the event's id — without a database.
/// </summary>
public sealed class FakeNotificationDataLayer : INotificationDataLayer
{
    public NotificationLog? Recorded { get; private set; }

    public Guid? MessageId { get; private set; }

    public Task RecordAsync(NotificationLog log, Guid messageId, CancellationToken cancellationToken = default)
    {
        Recorded = log;
        MessageId = messageId;
        return Task.CompletedTask;
    }
}
