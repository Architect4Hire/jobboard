using JobBoard.Audit.Core.Data;
using JobBoard.Audit.Core.Managers.Models.Domain;
using Xunit;

namespace JobBoard.Audit.Tests;

/// <summary>
/// Repository read side (SCRUB A6) over a real (SQLite) context: each filter axis narrows the trail, the
/// supplied filters AND-combine, and rows come back oldest-first so the caller reads a timeline.
/// </summary>
public sealed class AuditRepositoryQueryTests
{
    private static AuditEntry Entry(
        Guid? correlationId = null,
        Guid? subjectId = null,
        Guid? actorId = null,
        DateTime? occurredOnUtc = null,
        string eventType = "JobPosted") => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        CorrelationId = correlationId ?? Guid.NewGuid(),
        CausationId = Guid.NewGuid(),
        ActorId = actorId,
        SubjectId = subjectId,
        OccurredOnUtc = occurredOnUtc ?? DateTime.UtcNow,
        Payload = "{}",
    };

    private static async Task SeedAsync(AuditSqliteHarness harness, params AuditEntry[] entries)
    {
        await using var context = harness.CreateContext();
        context.AuditEntries.AddRange(entries);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task QueryAsync_ByCorrelationId_ReturnsOnlyThatRequestsFanOut()
    {
        using var harness = new AuditSqliteHarness();
        var correlationId = Guid.NewGuid();
        await SeedAsync(harness,
            Entry(correlationId: correlationId),
            Entry(correlationId: correlationId),
            Entry(correlationId: Guid.NewGuid())); // a different request

        await using var context = harness.CreateContext();
        var results = await new AuditRepository(context).QueryAsync(
            new AuditQuery(correlationId, null, null, null, null));

        Assert.Equal(2, results.Count);
        Assert.All(results, row => Assert.Equal(correlationId, row.CorrelationId));
    }

    [Fact]
    public async Task QueryAsync_BySubjectId_ReturnsThatEntitysLife()
    {
        using var harness = new AuditSqliteHarness();
        var subjectId = Guid.NewGuid();
        await SeedAsync(harness,
            Entry(subjectId: subjectId, eventType: "JobPosted"),
            Entry(subjectId: subjectId, eventType: "JobClosed"),
            Entry(subjectId: Guid.NewGuid()));

        await using var context = harness.CreateContext();
        var results = await new AuditRepository(context).QueryAsync(
            new AuditQuery(null, subjectId, null, null, null));

        Assert.Equal(2, results.Count);
        Assert.All(results, row => Assert.Equal(subjectId, row.SubjectId));
    }

    [Fact]
    public async Task QueryAsync_ByActorAndTimeWindow_AndCombinesFilters()
    {
        using var harness = new AuditSqliteHarness();
        var actorId = Guid.NewGuid();
        var anchor = new DateTime(2026, 07, 19, 12, 00, 00, DateTimeKind.Utc);
        await SeedAsync(harness,
            Entry(actorId: actorId, occurredOnUtc: anchor),                     // in window, right actor
            Entry(actorId: actorId, occurredOnUtc: anchor.AddHours(-2)),        // right actor, before window
            Entry(actorId: Guid.NewGuid(), occurredOnUtc: anchor));             // in window, wrong actor

        await using var context = harness.CreateContext();
        var results = await new AuditRepository(context).QueryAsync(
            new AuditQuery(null, null, actorId, anchor.AddHours(-1), anchor.AddHours(1)));

        var row = Assert.Single(results);
        Assert.Equal(actorId, row.ActorId);
        Assert.Equal(anchor, row.OccurredOnUtc);
    }

    [Fact]
    public async Task QueryAsync_OrdersOldestFirst()
    {
        using var harness = new AuditSqliteHarness();
        var correlationId = Guid.NewGuid();
        var anchor = new DateTime(2026, 07, 19, 12, 00, 00, DateTimeKind.Utc);
        await SeedAsync(harness,
            Entry(correlationId: correlationId, occurredOnUtc: anchor.AddMinutes(2)),
            Entry(correlationId: correlationId, occurredOnUtc: anchor),
            Entry(correlationId: correlationId, occurredOnUtc: anchor.AddMinutes(1)));

        await using var context = harness.CreateContext();
        var results = await new AuditRepository(context).QueryAsync(
            new AuditQuery(correlationId, null, null, null, null));

        Assert.Equal(
            new[] { anchor, anchor.AddMinutes(1), anchor.AddMinutes(2) },
            results.Select(row => row.OccurredOnUtc));
    }
}
