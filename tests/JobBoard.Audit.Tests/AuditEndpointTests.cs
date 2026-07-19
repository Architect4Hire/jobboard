using System.Net;
using System.Net.Http.Json;
using JobBoard.Audit.Core.Data;
using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Audit.Core.Managers.Models.ServiceModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobBoard.Audit.Tests;

/// <summary>
/// End-to-end over the real read pipeline (SCRUB A6): only service models come out, a valid filter returns
/// the matching trail rows oldest-first, and an all-empty filter is a 400 (never a whole-trail scan). Each
/// test hosts a fresh factory (its own in-memory database) for isolation.
/// </summary>
public sealed class AuditEndpointTests
{
    private static AuditEntry Entry(Guid correlationId, Guid subjectId, DateTime occurredOnUtc, string eventType) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        CorrelationId = correlationId,
        CausationId = Guid.NewGuid(),
        ActorId = Guid.NewGuid(),
        SubjectId = subjectId,
        OccurredOnUtc = occurredOnUtc,
        Payload = $$"""{"eventType":"{{eventType}}"}""",
    };

    private static async Task SeedAsync(AuditApiFactory factory, params AuditEntry[] entries)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        db.AuditEntries.AddRange(entries);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Query_ByCorrelationId_ReturnsThatRequestsTrail_OldestFirst()
    {
        using var factory = new AuditApiFactory();
        var client = factory.CreateClient();

        var correlationId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var anchor = new DateTime(2026, 07, 19, 12, 00, 00, DateTimeKind.Utc);
        await SeedAsync(factory,
            Entry(correlationId, jobId, anchor.AddMinutes(1), "JobClosed"),
            Entry(correlationId, jobId, anchor, "JobPosted"),
            Entry(Guid.NewGuid(), Guid.NewGuid(), anchor, "JobPosted")); // a different request

        var results = await client.GetFromJsonAsync<List<AuditEntryServiceModel>>(
            $"/audit/entries?correlationId={correlationId}");

        Assert.Equal(2, results!.Count);
        Assert.All(results, row => Assert.Equal(correlationId, row.CorrelationId));
        Assert.Equal(new[] { "JobPosted", "JobClosed" }, results.Select(row => row.EventType)); // oldest first
    }

    [Fact]
    public async Task Query_BySubjectId_ReturnsEntitysLife()
    {
        using var factory = new AuditApiFactory();
        var client = factory.CreateClient();

        var subjectId = Guid.NewGuid();
        var anchor = new DateTime(2026, 07, 19, 12, 00, 00, DateTimeKind.Utc);
        await SeedAsync(factory,
            Entry(Guid.NewGuid(), subjectId, anchor, "ApplicationSubmitted"),
            Entry(Guid.NewGuid(), subjectId, anchor.AddMinutes(5), "ApplicationStatusChanged"),
            Entry(Guid.NewGuid(), Guid.NewGuid(), anchor, "ApplicationSubmitted"));

        var results = await client.GetFromJsonAsync<List<AuditEntryServiceModel>>(
            $"/audit/entries?subjectId={subjectId}");

        Assert.Equal(2, results!.Count);
        Assert.All(results, row => Assert.Equal(subjectId, row.SubjectId));
    }

    [Fact]
    public async Task Query_EmptyFilter_Returns400()
    {
        using var factory = new AuditApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/audit/entries");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
