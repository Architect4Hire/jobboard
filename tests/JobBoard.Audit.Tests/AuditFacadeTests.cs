using FluentValidation;
using JobBoard.Audit.Core.Business;
using JobBoard.Audit.Core.Facade;
using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Audit.Core.Managers.Models.ViewModels;
using JobBoard.Audit.Core.Managers.Validators;
using JobBoard.Audit.Tests.Fakes;
using Xunit;

namespace JobBoard.Audit.Tests;

/// <summary>
/// The facade owns the one edge seam the layers below must not: validating the support-query filter
/// (SCRUB A6). An all-empty filter and an inverted time window are rejected before any read; a valid filter
/// flows through business (mapping rows to service models) and reaches the data layer as a domain query.
/// </summary>
public sealed class AuditFacadeTests
{
    private static (AuditFacade Facade, FakeAuditDataLayer DataLayer) CreateFacade()
    {
        var dataLayer = new FakeAuditDataLayer();
        var facade = new AuditFacade(new AuditBusiness(dataLayer), new AuditQueryViewModelValidator());
        return (facade, dataLayer);
    }

    [Fact]
    public async Task QueryAsync_EmptyFilter_ThrowsAndNeverReads()
    {
        var (facade, dataLayer) = CreateFacade();

        await Assert.ThrowsAsync<ValidationException>(() => facade.QueryAsync(new AuditQueryViewModel()));

        Assert.Null(dataLayer.Queried); // rejected at the edge, before any query ran
    }

    [Fact]
    public async Task QueryAsync_InvertedTimeWindow_Throws()
    {
        var (facade, _) = CreateFacade();
        var query = new AuditQueryViewModel
        {
            FromUtc = new DateTime(2026, 07, 19, 13, 00, 00, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 07, 19, 12, 00, 00, DateTimeKind.Utc),
        };

        await Assert.ThrowsAsync<ValidationException>(() => facade.QueryAsync(query));
    }

    [Fact]
    public async Task QueryAsync_ValidFilter_MapsRowsToServiceModels_AndPassesDomainQuery()
    {
        var (facade, dataLayer) = CreateFacade();
        var correlationId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        dataLayer.QueryResult =
        [
            new AuditEntry
            {
                Id = Guid.NewGuid(),
                EventType = "JobPosted",
                CorrelationId = correlationId,
                CausationId = Guid.NewGuid(),
                ActorId = actorId,
                SubjectId = subjectId,
                OccurredOnUtc = DateTime.UtcNow,
                Payload = """{"title":"Engineer"}""",
            },
        ];

        var results = await facade.QueryAsync(new AuditQueryViewModel { CorrelationId = correlationId });

        // The validated view-model filter reached the data layer as a domain query.
        Assert.Equal(correlationId, dataLayer.Queried!.CorrelationId);

        // The row was projected to a service model with the payload parsed to structured JSON.
        var model = Assert.Single(results);
        Assert.Equal("JobPosted", model.EventType);
        Assert.Equal(actorId, model.ActorId);
        Assert.Equal(subjectId, model.SubjectId);
        Assert.Equal("Engineer", model.Payload.GetProperty("title").GetString());
    }
}
