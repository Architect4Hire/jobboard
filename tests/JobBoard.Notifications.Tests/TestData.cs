using JobBoard.Contracts;
using JobBoard.Notifications.Core.Managers.Models.Domain;

namespace JobBoard.Notifications.Tests;

/// <summary>Builders for the events and fixtures the Notifications tests share.</summary>
internal static class TestData
{
    public static ApplicationSubmitted ApplicationSubmitted(Guid? id = null, Guid? candidateId = null, Guid? jobId = null) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), candidateId ?? Guid.NewGuid(), jobId ?? Guid.NewGuid(), DateTime.UtcNow);

    public static ApplicationStatusChanged ApplicationStatusChanged(
        Guid? id = null,
        Guid? candidateId = null,
        string from = "Submitted",
        string to = "Reviewed") =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), candidateId ?? Guid.NewGuid(), Guid.NewGuid(), from, to, DateTime.UtcNow);

    public static JobPosted JobPosted(Guid? id = null, Guid? employerId = null, string title = "Engineer", string location = "Remote") =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), employerId ?? Guid.NewGuid(), title, location, DateTime.UtcNow);

    public static NotificationLog NotificationLog(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        RecipientId = Guid.NewGuid(),
        Kind = "Test",
        Subject = "Subject",
        Body = "Body",
        CreatedOnUtc = DateTime.UtcNow,
    };
}
