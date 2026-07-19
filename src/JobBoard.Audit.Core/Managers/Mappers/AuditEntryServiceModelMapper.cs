using System.Text.Json;
using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Audit.Core.Managers.Models.ServiceModels;

namespace JobBoard.Audit.Core.Managers.Mappers;

/// <summary>
/// Projects an immutable <see cref="AuditEntry"/> row into the outbound <see cref="AuditEntryServiceModel"/>
/// (the read side of the trail — SCRUB A6). The counterpart to <see cref="AuditEntryMapper"/>, which builds
/// the row on the way in. The stored <c>jsonb</c> payload is parsed back into structured JSON so the caller
/// gets the event detail as JSON, never the raw column string.
/// </summary>
public static class AuditEntryServiceModelMapper
{
    // Match the write side (AuditEntryMapper) so the payload round-trips through the same web-defaults shape.
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public static AuditEntryServiceModel ToServiceModel(this AuditEntry entry) =>
        new(
            entry.Id,
            entry.EventType,
            entry.CorrelationId,
            entry.CausationId,
            entry.ActorId,
            entry.SubjectId,
            entry.OccurredOnUtc,
            // Deserialize to a self-contained JsonElement (its own backing document) so it stays valid after
            // this method returns — the payload was written as valid JSON by the consumer that recorded it.
            JsonSerializer.Deserialize<JsonElement>(entry.Payload, PayloadOptions));
}
