using JobBoard.Contracts;
using JobBoard.Notifications.Core.Managers.Models.Domain;

namespace JobBoard.Notifications.Core.Managers.Mappers;

/// <summary>
/// Turns each consumed integration event into a <see cref="NotificationLog"/> — the recipient, a kind
/// tag, and a rendered subject/body. This is the Notifications domain's whole job: compose the message a
/// consumer will (in a fuller system) deliver. Each stamps a fresh <see cref="NotificationLog.Id"/>.
/// </summary>
public static class NotificationMappers
{
    public static NotificationLog ToNotificationLog(this ApplicationSubmitted @event) => new()
    {
        Id = Guid.NewGuid(),
        RecipientId = @event.CandidateId,
        Kind = nameof(ApplicationSubmitted),
        Subject = "Application received",
        Body = $"Your application to job {@event.JobId} was received on {@event.SubmittedOnUtc:u}.",
        CreatedOnUtc = DateTime.UtcNow,
    };

    public static NotificationLog ToNotificationLog(this ApplicationStatusChanged @event) => new()
    {
        Id = Guid.NewGuid(),
        RecipientId = @event.CandidateId,
        Kind = nameof(ApplicationStatusChanged),
        Subject = $"Application {@event.ToStatus}",
        Body = $"Your application to job {@event.JobId} changed from {@event.FromStatus} to {@event.ToStatus}.",
        CreatedOnUtc = DateTime.UtcNow,
    };

    public static NotificationLog ToNotificationLog(this JobPosted @event) => new()
    {
        Id = Guid.NewGuid(),
        RecipientId = @event.EmployerId,
        Kind = nameof(JobPosted),
        Subject = "Job posted",
        Body = $"Your job '{@event.Title}' ({@event.Location}) is now live.",
        CreatedOnUtc = DateTime.UtcNow,
    };
}
