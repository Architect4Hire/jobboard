using System.Net;
using System.Net.Http.Json;
using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Shared.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobBoard.Applications.Tests;

/// <summary>
/// End-to-end over the real pipeline: only view models go in, only service models come out, and each state
/// change writes its outbox row. Each test hosts a fresh factory (its own in-memory database) for isolation.
/// </summary>
public sealed class ApplicationsEndpointTests
{
    [Fact]
    public async Task Submit_Then_Get_ReturnsCreatedApplication_AndWritesOutboxRow()
    {
        using var factory = new ApplicationsApiFactory();
        var client = factory.CreateClient();

        var submit = await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel());

        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var created = await submit.Content.ReadFromJsonAsync<ApplicationDetailServiceModel>();
        Assert.NotNull(created);
        Assert.Equal(ApplicationStatus.Submitted, created!.Status);

        var get = await client.GetAsync($"/applications/{created.Id}");
        get.EnsureSuccessStatusCode();
        var fetched = await get.Content.ReadFromJsonAsync<ApplicationDetailServiceModel>();
        Assert.Equal(created.Id, fetched!.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        var row = await db.OutboxMessages.SingleAsync();
        Assert.Equal("ApplicationSubmitted", row.Type);
        Assert.Equal("ApplicationSubmitted", row.Destination);
        Assert.Null(row.ProcessedOnUtc);   // the dispatcher (not running in tests) would stamp this
    }

    [Fact]
    public async Task Submit_InvalidBody_Returns400()
    {
        using var factory = new ApplicationsApiFactory();
        var client = factory.CreateClient();

        var submit = await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel(candidateId: Guid.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
    }

    [Fact]
    public async Task Submit_DuplicateCandidateAndJob_Returns409()
    {
        using var factory = new ApplicationsApiFactory();
        var client = factory.CreateClient();

        var candidateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var first = await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel(candidateId: candidateId, jobId: jobId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel(candidateId: candidateId, jobId: jobId));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Withdraw_WithdrawsApplication_WritesStatusChangedRow_AndIsConflictOnRepeat()
    {
        using var factory = new ApplicationsApiFactory();
        var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel()))
            .Content.ReadFromJsonAsync<ApplicationDetailServiceModel>();

        var withdraw = await client.PostAsync($"/applications/{created!.Id}/withdraw", content: null);

        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
        var withdrawn = await withdraw.Content.ReadFromJsonAsync<ApplicationDetailServiceModel>();
        Assert.Equal(ApplicationStatus.Withdrawn, withdrawn!.Status);

        // A withdrawn application is terminal — withdrawing again is a 409, and no second event.
        var again = await client.PostAsync($"/applications/{created.Id}/withdraw", content: null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        // One ApplicationSubmitted (from submit) + one ApplicationStatusChanged (from withdraw).
        Assert.Equal(1, await db.OutboxMessages.CountAsync(r => r.Type == "ApplicationStatusChanged"));
    }

    [Fact]
    public async Task Advance_LegalTransition_Succeeds_IllegalIsConflict()
    {
        using var factory = new ApplicationsApiFactory();
        var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel()))
            .Content.ReadFromJsonAsync<ApplicationDetailServiceModel>();

        // Submitted → Reviewed is legal.
        var advance = await client.PostAsJsonAsync(
            $"/applications/{created!.Id}/advance", TestData.AdvanceViewModel(ApplicationStatus.Reviewed));
        Assert.Equal(HttpStatusCode.OK, advance.StatusCode);
        var advanced = await advance.Content.ReadFromJsonAsync<ApplicationDetailServiceModel>();
        Assert.Equal(ApplicationStatus.Reviewed, advanced!.Status);

        // Reviewed → Submitted is not a legal advance → 409.
        var illegal = await client.PostAsJsonAsync(
            $"/applications/{created.Id}/advance", TestData.AdvanceViewModel(ApplicationStatus.Submitted));
        Assert.Equal(HttpStatusCode.Conflict, illegal.StatusCode);
    }

    [Fact]
    public async Task Advance_UndefinedTargetStatus_Returns400()
    {
        using var factory = new ApplicationsApiFactory();
        var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel()))
            .Content.ReadFromJsonAsync<ApplicationDetailServiceModel>();

        var advance = await client.PostAsJsonAsync(
            $"/applications/{created!.Id}/advance", TestData.AdvanceViewModel((ApplicationStatus)99));
        Assert.Equal(HttpStatusCode.BadRequest, advance.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsApplicationsForCandidate()
    {
        using var factory = new ApplicationsApiFactory();
        var client = factory.CreateClient();

        var candidateId = Guid.NewGuid();
        await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel(candidateId: candidateId));
        await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel(candidateId: candidateId));
        await client.PostAsJsonAsync("/applications", TestData.SubmitViewModel()); // a different candidate

        var results = await client.GetFromJsonAsync<List<ApplicationSummaryServiceModel>>(
            $"/applications?candidateId={candidateId}");

        Assert.Equal(2, results!.Count);
    }

    [Fact]
    public async Task Mine_ReturnsOnlyTheAuthenticatedCandidatesApplications_EnrichedWithJobAndEmployer()
    {
        using var factory = new ApplicationsApiFactory();
        var candidateId = Guid.NewGuid();

        // Seed reference data as the two consumers would, then an application for this candidate and one
        // for a different candidate — the endpoint must only ever return the caller's own rows.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
            var jobId = Guid.NewGuid();
            var employerId = Guid.NewGuid();
            db.JobReferences.Add(new JobReference { JobId = jobId, Title = "Senior Engineer", EmployerId = employerId });
            db.EmployerReferences.Add(new EmployerReference { EmployerId = employerId, CompanyName = "Acme Co" });
            db.Applications.Add(new Application
            {
                Id = Guid.NewGuid(),
                CandidateId = candidateId,
                JobId = jobId,
                Status = ApplicationStatus.Submitted,
                SubmittedOnUtc = DateTime.UtcNow,
                StatusChangedOnUtc = DateTime.UtcNow,
            });
            db.Applications.Add(new Application
            {
                Id = Guid.NewGuid(),
                CandidateId = Guid.NewGuid(), // a different candidate
                JobId = jobId,
                Status = ApplicationStatus.Submitted,
                SubmittedOnUtc = DateTime.UtcNow,
                StatusChangedOnUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AuditHeaderNames.UserId, candidateId.ToString());

        var results = await client.GetFromJsonAsync<List<ApplicationHistoryServiceModel>>("/applications/mine");

        var result = Assert.Single(results!);
        Assert.Equal("Senior Engineer", result.JobTitle);
        Assert.Equal("Acme Co", result.EmployerName);
    }

    [Fact]
    public async Task Mine_WithNoAuthenticatedCandidate_Returns401()
    {
        using var factory = new ApplicationsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/applications/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
