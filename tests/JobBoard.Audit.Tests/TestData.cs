using JobBoard.Contracts;

namespace JobBoard.Audit.Tests;

/// <summary>Builders for the events the Audit tests share. Each lets a test pin the thread fields
/// (id/correlation/causation/actor) it wants to assert on, defaulting the rest.</summary>
internal static class TestData
{
    public static JobPosted JobPosted(
        Guid? id = null,
        Guid? jobId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        Guid? actorId = null,
        DateTime? postedOnUtc = null) =>
        new(id ?? Guid.NewGuid(), jobId ?? Guid.NewGuid(), Guid.NewGuid(), "Engineer", "Remote",
            postedOnUtc ?? DateTime.UtcNow)
        {
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CausationId = causationId ?? Guid.NewGuid(),
            ActorId = actorId,
        };

    public static JobClosed JobClosed(
        Guid? id = null,
        Guid? jobId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        Guid? actorId = null,
        DateTime? closedOnUtc = null) =>
        new(id ?? Guid.NewGuid(), jobId ?? Guid.NewGuid(), Guid.NewGuid(), closedOnUtc ?? DateTime.UtcNow)
        {
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CausationId = causationId ?? Guid.NewGuid(),
            ActorId = actorId,
        };

    public static ApplicationSubmitted ApplicationSubmitted(
        Guid? id = null,
        Guid? applicationId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        Guid? actorId = null,
        DateTime? submittedOnUtc = null) =>
        new(id ?? Guid.NewGuid(), applicationId ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            submittedOnUtc ?? DateTime.UtcNow)
        {
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CausationId = causationId ?? Guid.NewGuid(),
            ActorId = actorId,
        };

    public static ApplicationStatusChanged ApplicationStatusChanged(
        Guid? id = null,
        Guid? applicationId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        Guid? actorId = null,
        string from = "Submitted",
        string to = "Reviewed",
        DateTime? changedOnUtc = null) =>
        new(id ?? Guid.NewGuid(), applicationId ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), from, to,
            changedOnUtc ?? DateTime.UtcNow)
        {
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CausationId = causationId ?? Guid.NewGuid(),
            ActorId = actorId,
        };
}
