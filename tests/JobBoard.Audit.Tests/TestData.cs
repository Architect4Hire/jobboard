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

    public static AccountCreated AccountCreated(
        Guid? id = null,
        Guid? accountId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        Guid? actorId = null,
        string email = "user@example.com",
        string role = "Candidate",
        DateTime? occurredOnUtc = null)
    {
        var account = accountId ?? Guid.NewGuid();
        return new(id ?? Guid.NewGuid(), account, email, role, occurredOnUtc ?? DateTime.UtcNow)
        {
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CausationId = causationId ?? Guid.NewGuid(),
            // Registration self-attributes: the actor is the account itself unless a test overrides it.
            ActorId = actorId ?? account,
        };
    }

    public static LoggedIn LoggedIn(
        Guid? id = null,
        Guid? accountId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        Guid? actorId = null,
        string email = "user@example.com",
        string role = "Candidate",
        DateTime? occurredOnUtc = null)
    {
        var account = accountId ?? Guid.NewGuid();
        return new(id ?? Guid.NewGuid(), account, email, role, occurredOnUtc ?? DateTime.UtcNow)
        {
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CausationId = causationId ?? Guid.NewGuid(),
            // Login self-attributes, same as registration.
            ActorId = actorId ?? account,
        };
    }

    public static LoginFailed LoginFailed(
        Guid? id = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        string email = "user@example.com",
        string reason = "invalid_credentials",
        DateTime? occurredOnUtc = null) =>
        new(id ?? Guid.NewGuid(), email, reason, occurredOnUtc ?? DateTime.UtcNow)
        {
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CausationId = causationId ?? Guid.NewGuid(),
            // No authenticated actor for a rejected login.
            ActorId = null,
        };

    public static ProfileUpdated ProfileUpdated(
        Guid? id = null,
        Guid? profileId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        Guid? actorId = null,
        string profileType = "Candidate",
        DateTime? occurredOnUtc = null) =>
        new(id ?? Guid.NewGuid(), profileId ?? Guid.NewGuid(), profileType, occurredOnUtc ?? DateTime.UtcNow)
        {
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CausationId = causationId ?? Guid.NewGuid(),
            ActorId = actorId ?? Guid.NewGuid(),
        };
}
