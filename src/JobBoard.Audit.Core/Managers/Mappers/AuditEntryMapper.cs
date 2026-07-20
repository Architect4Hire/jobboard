using System.Text.Json;
using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Contracts;

namespace JobBoard.Audit.Core.Managers.Mappers;

/// <summary>
/// Turns any consumed <see cref="IIntegrationEvent"/> into one <see cref="AuditEntry"/> — the whole job of
/// the Audit domain. The trail's row shape is uniform, so this is a single generic sink rather than a
/// mapper per event: the thread (<see cref="IIntegrationEvent.CorrelationId"/>,
/// <see cref="IIntegrationEvent.CausationId"/>, <see cref="IIntegrationEvent.ActorId"/>) and the identity
/// (<see cref="IIntegrationEvent.Id"/>, the type name) come straight off the interface, and the full event
/// is serialized to the <c>jsonb</c> payload.
/// </summary>
public static class AuditEntryMapper
{
    // Match Outbox/IntegrationEventProcessor so the recorded payload is byte-identical to what shipped.
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public static AuditEntry ToAuditEntry(this IIntegrationEvent @event)
    {
        var (subjectId, occurredOnUtc) = Describe(@event);

        return new AuditEntry
        {
            // The event id is the row key: a redelivery collides on the PK and lines up with the inbox key.
            Id = @event.Id,
            EventType = @event.GetType().Name,
            CorrelationId = @event.CorrelationId,
            CausationId = @event.CausationId,
            ActorId = @event.ActorId,
            SubjectId = subjectId,
            OccurredOnUtc = occurredOnUtc,
            // Serialize the concrete runtime type so every field is captured, not just the interface's.
            Payload = JsonSerializer.Serialize(@event, @event.GetType(), PayloadOptions),
        };
    }

    /// <summary>
    /// The two fields the trail needs that aren't on <see cref="IIntegrationEvent"/>: the principal entity
    /// (indexed for "an entity's life" queries) and when the event actually occurred (from the event, not
    /// the row write). These live on each concrete record, so this is the one spot that knows the shapes —
    /// extended once per new audited event (SCRUB A7). An unmapped event is a wiring mistake (Audit was
    /// subscribed to a topic no one taught the trail to read), so fail loud rather than record a bogus row.
    /// </summary>
    private static (Guid? SubjectId, DateTime OccurredOnUtc) Describe(IIntegrationEvent @event) => @event switch
    {
        JobPosted e => (e.JobId, e.PostedOnUtc),
        JobClosed e => (e.JobId, e.ClosedOnUtc),
        ApplicationSubmitted e => (e.ApplicationId, e.SubmittedOnUtc),
        ApplicationStatusChanged e => (e.ApplicationId, e.ChangedOnUtc),
        AccountCreated e => (e.AccountId, e.OccurredOnUtc),
        LoggedIn e => (e.AccountId, e.OccurredOnUtc),
        // A failed login has no account id (an unknown email has no account, and we don't disclose which):
        // no subject, just the attempt in the trail.
        LoginFailed e => ((Guid?)null, e.OccurredOnUtc),
        ProfileUpdated e => (e.ProfileId, e.OccurredOnUtc),
        _ => throw new NotSupportedException(
            $"No audit projection for event '{@event.GetType().Name}'. Add its subject id and occurred-at " +
            "to AuditEntryMapper.Describe when auditing a new event (SCRUB A7)."),
    };
}
