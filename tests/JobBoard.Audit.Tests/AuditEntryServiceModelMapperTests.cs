using System.Text.Json;
using JobBoard.Audit.Core.Managers.Mappers;
using JobBoard.Audit.Core.Managers.Models.Domain;
using Xunit;

namespace JobBoard.Audit.Tests;

/// <summary>
/// The read-side projection (SCRUB A6): a trail row becomes a service model with its thread fields copied
/// through and the stored <c>jsonb</c> string parsed back into structured JSON — never the raw column text.
/// </summary>
public sealed class AuditEntryServiceModelMapperTests
{
    [Fact]
    public void ToServiceModel_ParsesPayload_AndCopiesThreadFields()
    {
        var entry = new AuditEntry
        {
            Id = Guid.NewGuid(),
            EventType = "ApplicationStatusChanged",
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            OccurredOnUtc = new DateTime(2026, 07, 19, 12, 00, 00, DateTimeKind.Utc),
            Payload = """{"from":"Submitted","to":"Reviewed"}""",
        };

        var model = entry.ToServiceModel();

        Assert.Equal(entry.Id, model.Id);
        Assert.Equal(entry.EventType, model.EventType);
        Assert.Equal(entry.CorrelationId, model.CorrelationId);
        Assert.Equal(entry.CausationId, model.CausationId);
        Assert.Equal(entry.ActorId, model.ActorId);
        Assert.Equal(entry.SubjectId, model.SubjectId);
        Assert.Equal(entry.OccurredOnUtc, model.OccurredOnUtc);

        Assert.Equal(JsonValueKind.Object, model.Payload.ValueKind);
        Assert.Equal("Reviewed", model.Payload.GetProperty("to").GetString());
    }

    [Fact]
    public void ToServiceModel_PreservesNullActorAndSubject()
    {
        var entry = new AuditEntry
        {
            Id = Guid.NewGuid(),
            EventType = "AccountRegistered",
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ActorId = null,     // anonymous cradle event
            SubjectId = null,
            OccurredOnUtc = DateTime.UtcNow,
            Payload = "{}",
        };

        var model = entry.ToServiceModel();

        Assert.Null(model.ActorId);
        Assert.Null(model.SubjectId);
    }
}
