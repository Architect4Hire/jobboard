using System.Text.Json;
using JobBoard.Audit.Core.Managers.Mappers;
using JobBoard.Contracts;
using Xunit;

namespace JobBoard.Audit.Tests;

/// <summary>
/// The generic sink's projection: every event maps to one row carrying the thread off the interface, the
/// per-type subject id and occurred-at, and the full event as the jsonb payload. An event the trail was
/// never taught to read fails loud rather than recording a bogus row.
/// </summary>
public sealed class AuditEntryMapperTests
{
    [Fact]
    public void JobPosted_MapsThread_SubjectIsJob_AndPayloadRoundTrips()
    {
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var postedOnUtc = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
        var @event = TestData.JobPosted(jobId: jobId, correlationId: correlationId, causationId: causationId,
            actorId: actorId, postedOnUtc: postedOnUtc);

        var entry = @event.ToAuditEntry();

        Assert.Equal(@event.Id, entry.Id);
        Assert.Equal("JobPosted", entry.EventType);
        Assert.Equal(correlationId, entry.CorrelationId);
        Assert.Equal(causationId, entry.CausationId);
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal(jobId, entry.SubjectId);            // the posting is the subject
        Assert.Equal(postedOnUtc, entry.OccurredOnUtc);  // from the event, not the row write

        // Payload is the full event: it round-trips back to an equal record.
        var roundTripped = JsonSerializer.Deserialize<JobPosted>(entry.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(@event, roundTripped);
    }

    [Fact]
    public void JobClosed_SubjectIsJob_AndOccurredIsClosedOn()
    {
        var jobId = Guid.NewGuid();
        var closedOnUtc = new DateTime(2026, 7, 19, 11, 0, 0, DateTimeKind.Utc);
        var @event = TestData.JobClosed(jobId: jobId, closedOnUtc: closedOnUtc);

        var entry = @event.ToAuditEntry();

        Assert.Equal("JobClosed", entry.EventType);
        Assert.Equal(jobId, entry.SubjectId);
        Assert.Equal(closedOnUtc, entry.OccurredOnUtc);
    }

    [Fact]
    public void ApplicationSubmitted_SubjectIsApplication_AndOccurredIsSubmittedOn()
    {
        var applicationId = Guid.NewGuid();
        var submittedOnUtc = new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
        var @event = TestData.ApplicationSubmitted(applicationId: applicationId, submittedOnUtc: submittedOnUtc);

        var entry = @event.ToAuditEntry();

        Assert.Equal("ApplicationSubmitted", entry.EventType);
        Assert.Equal(applicationId, entry.SubjectId);
        Assert.Equal(submittedOnUtc, entry.OccurredOnUtc);
    }

    [Fact]
    public void ApplicationStatusChanged_SubjectIsApplication_AndOccurredIsChangedOn()
    {
        var applicationId = Guid.NewGuid();
        var changedOnUtc = new DateTime(2026, 7, 19, 13, 0, 0, DateTimeKind.Utc);
        var @event = TestData.ApplicationStatusChanged(applicationId: applicationId, changedOnUtc: changedOnUtc);

        var entry = @event.ToAuditEntry();

        Assert.Equal("ApplicationStatusChanged", entry.EventType);
        Assert.Equal(applicationId, entry.SubjectId);
        Assert.Equal(changedOnUtc, entry.OccurredOnUtc);
    }

    [Fact]
    public void AnonymousEvent_MapsNullActor()
    {
        var @event = TestData.JobPosted(actorId: null);

        var entry = @event.ToAuditEntry();

        Assert.Null(entry.ActorId);
    }

    [Fact]
    public void UnmappedEvent_FailsLoud()
    {
        var @event = new UnknownEvent(Guid.NewGuid());

        Assert.Throws<NotSupportedException>(() => @event.ToAuditEntry());
    }

    // An event the projection was never extended for (stands in for "Audit subscribed to a topic no one
    // taught the trail to read"). Recording it with a bogus subject/occurred-at would be worse than failing.
    private sealed record UnknownEvent(Guid Id) : IIntegrationEvent
    {
        public Guid CorrelationId { get; init; }
        public Guid CausationId { get; init; }
        public Guid? ActorId { get; init; }
    }
}
