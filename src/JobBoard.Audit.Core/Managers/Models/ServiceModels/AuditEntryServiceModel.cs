using System.Text.Json;

namespace JobBoard.Audit.Core.Managers.Models.ServiceModels;

/// <summary>
/// The support-query row shape returned by the audit read surface (SCRUB A6) — the outbound projection of
/// one immutable <see cref="Domain.AuditEntry"/>. Carries the thread (<see cref="CorrelationId"/>,
/// <see cref="CausationId"/>, <see cref="ActorId"/>) so support can reconstruct the causal timeline, plus
/// the event detail as parsed JSON. Deliberately a ServiceModel, never the EF row: only ServiceModels leave
/// a service, and <c>auditdb</c> is never exposed directly (.claude/rules/audit.md).
/// </summary>
/// <param name="Payload">The recorded event as structured JSON, not the raw <c>jsonb</c> string. It was
/// already sanitized of secrets/PII at write time (audit.md), so it is the event detail support reads in
/// the <c>trace-a-request</c> skill.</param>
public sealed record AuditEntryServiceModel(
    Guid Id,
    string EventType,
    Guid CorrelationId,
    Guid CausationId,
    Guid? ActorId,
    Guid? SubjectId,
    DateTime OccurredOnUtc,
    JsonElement Payload);
