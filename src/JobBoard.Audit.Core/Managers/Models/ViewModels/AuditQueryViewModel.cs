namespace JobBoard.Audit.Core.Managers.Models.ViewModels;

/// <summary>
/// The inbound filter for the support-query surface (SCRUB A6), bound from the query string. Every field is
/// optional and the supplied ones are AND-combined, so one endpoint serves all four <c>trace-a-request</c>
/// axes and their combinations — a request's whole fan-out (<see cref="CorrelationId"/>), one entity's life
/// (<see cref="SubjectId"/>), everything an actor did (<see cref="ActorId"/>), and a time window
/// (<see cref="FromUtc"/>/<see cref="ToUtc"/>). The validator rejects an all-empty filter so a query never
/// scans the whole trail.
/// </summary>
public sealed record AuditQueryViewModel
{
    /// <summary>The originating request's thread — returns its whole fan-out across every service.</summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>The principal entity (job, application, …) — returns that entity's entire life.</summary>
    public Guid? SubjectId { get; init; }

    /// <summary>The acting identity — returns everything that actor did (narrow with a time window).</summary>
    public Guid? ActorId { get; init; }

    /// <summary>Inclusive lower bound on when the event occurred (UTC).</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Inclusive upper bound on when the event occurred (UTC).</summary>
    public DateTime? ToUtc { get; init; }
}
