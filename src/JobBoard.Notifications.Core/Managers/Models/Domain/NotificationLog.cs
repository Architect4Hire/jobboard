namespace JobBoard.Notifications.Core.Managers.Models.Domain;

/// <summary>
/// A recorded notification — the aggregate of the Notifications context. Each consumed integration event
/// produces one row describing the message that would be delivered (this PoC logs rather than sending
/// email). <see cref="RecipientId"/> is the account the notification is for (candidate or employer),
/// duplicated from the event — never a cross-service FK.
/// </summary>
public class NotificationLog
{
    public Guid Id { get; set; }

    /// <summary>The account the notification targets (candidate or employer), from the source event.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>Which event produced this notification (e.g. <c>JobPosted</c>).</summary>
    public string Kind { get; set; } = default!;

    public string Subject { get; set; } = default!;

    public string Body { get; set; } = default!;

    public DateTime CreatedOnUtc { get; set; }
}
