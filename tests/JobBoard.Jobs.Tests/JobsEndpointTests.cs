using System.Net;
using System.Net.Http.Json;
using JobBoard.Jobs.Core.Data;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobBoard.Jobs.Tests;

/// <summary>
/// End-to-end over the real pipeline: only view models go in, only service models come out, and the
/// close path writes its outbox row. Each test hosts a fresh factory (its own in-memory database) for
/// isolation.
/// </summary>
public sealed class JobsEndpointTests
{
    [Fact]
    public async Task Post_Then_Get_ReturnsCreatedJob()
    {
        using var factory = new JobsApiFactory();
        var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync("/jobs", TestData.PostViewModel(title: "API Engineer"));

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<JobDetailServiceModel>();
        Assert.NotNull(created);
        Assert.Equal("API Engineer", created!.Title);
        Assert.Equal(JobStatus.Open, created.Status);
        Assert.Equal("engineering", Assert.Single(created.Categories).Slug);

        var get = await client.GetAsync($"/jobs/{created.Id}");
        get.EnsureSuccessStatusCode();
        var fetched = await get.Content.ReadFromJsonAsync<JobDetailServiceModel>();
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task Post_InvalidBody_Returns400()
    {
        using var factory = new JobsApiFactory();
        var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync("/jobs", TestData.PostViewModel(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByCategorySlug()
    {
        using var factory = new JobsApiFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/jobs", TestData.PostViewModel(
            title: "Eng job",
            categories: [new JobClassificationViewModel { Name = "Engineering", Slug = "engineering" }]));
        await client.PostAsJsonAsync("/jobs", TestData.PostViewModel(
            title: "Design job",
            categories: [new JobClassificationViewModel { Name = "Design", Slug = "design" }]));

        var results = await client.GetFromJsonAsync<List<JobSummaryServiceModel>>("/jobs?category=design");

        Assert.Equal("Design job", Assert.Single(results!).Title);
    }

    [Fact]
    public async Task Get_MissingJob_Returns404()
    {
        using var factory = new JobsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/jobs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Close_ClosesJob_WritesJobClosedOutboxRow_AndIsConflictOnRepeat()
    {
        using var factory = new JobsApiFactory();
        var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/jobs", TestData.PostViewModel()))
            .Content.ReadFromJsonAsync<JobDetailServiceModel>();

        var close = await client.PostAsync($"/jobs/{created!.Id}/close", content: null);

        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var closed = await close.Content.ReadFromJsonAsync<JobDetailServiceModel>();
        Assert.Equal(JobStatus.Closed, closed!.Status);

        // The event was enqueued to the outbox in the same transaction as the status change.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            var row = await db.OutboxMessages.SingleAsync();
            Assert.Equal("JobClosed", row.Type);
            Assert.Equal("JobClosed", row.Destination);
            Assert.Null(row.ProcessedOnUtc);   // the dispatcher (not running in tests) would stamp this
        }

        // Domain rule: an already-closed job cannot be closed again → 409, and no second event.
        var again = await client.PostAsync($"/jobs/{created.Id}/close", content: null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            Assert.Equal(1, await db.OutboxMessages.CountAsync());
        }
    }
}
