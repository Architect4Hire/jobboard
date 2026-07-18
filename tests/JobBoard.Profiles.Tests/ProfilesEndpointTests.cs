using System.Net;
using System.Net.Http.Json;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using Xunit;

namespace JobBoard.Profiles.Tests;

/// <summary>
/// End-to-end over the real pipeline: only view models go in, only service models come out. Covers the
/// upsert lifecycle (GET 404 → PUT create → GET returns it → PUT update) for both aggregates, the skills
/// round-trip, and a validation failure. Each test hosts a fresh factory (its own in-memory database).
/// </summary>
public sealed class ProfilesEndpointTests
{
    [Fact]
    public async Task Candidate_Upsert_Get_Update_Lifecycle()
    {
        using var factory = new ProfilesApiFactory();
        var client = factory.CreateClient();
        var candidateId = Guid.NewGuid();

        // Not there yet.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/profiles/candidates/{candidateId}")).StatusCode);

        // Create.
        var create = await client.PutAsJsonAsync(
            $"/profiles/candidates/{candidateId}",
            TestData.CandidateViewModel(headline: "SRE", skills: ["linux", "go"]));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CandidateProfileServiceModel>();
        Assert.Equal(candidateId, created!.CandidateId);
        Assert.Equal(["linux", "go"], created.Skills);

        // Read it back — skills survived the value-converter round-trip.
        var fetched = await client.GetFromJsonAsync<CandidateProfileServiceModel>($"/profiles/candidates/{candidateId}");
        Assert.Equal("SRE", fetched!.Headline);
        Assert.Equal(["linux", "go"], fetched.Skills);

        // Update the same owner — replaced, not duplicated.
        var update = await client.PutAsJsonAsync(
            $"/profiles/candidates/{candidateId}",
            TestData.CandidateViewModel(headline: "Principal SRE", skills: ["k8s"]));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await client.GetFromJsonAsync<CandidateProfileServiceModel>($"/profiles/candidates/{candidateId}");
        Assert.Equal("Principal SRE", updated!.Headline);
        Assert.Equal(["k8s"], updated.Skills);
    }

    [Fact]
    public async Task Candidate_Upsert_InvalidBody_Returns400()
    {
        using var factory = new ProfilesApiFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/profiles/candidates/{Guid.NewGuid()}",
            TestData.CandidateViewModel(headline: "")); // empty headline fails validation

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Candidate_Upsert_NullSkills_Returns400_Not500()
    {
        using var factory = new ProfilesApiFactory();
        var client = factory.CreateClient();

        // Explicit "skills": null in the body — must be a validation 400, never an unhandled 500.
        var response = await client.PutAsJsonAsync(
            $"/profiles/candidates/{Guid.NewGuid()}",
            new { headline = "Dev", summary = "Summary", skills = (string[]?)null, resumeUrl = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Candidate_Upsert_EmptySkills_RoundTripsAsEmpty()
    {
        using var factory = new ProfilesApiFactory();
        var client = factory.CreateClient();
        var candidateId = Guid.NewGuid();

        var create = await client.PutAsJsonAsync(
            $"/profiles/candidates/{candidateId}",
            TestData.CandidateViewModel(skills: []));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var fetched = await client.GetFromJsonAsync<CandidateProfileServiceModel>($"/profiles/candidates/{candidateId}");
        Assert.Empty(fetched!.Skills); // [] -> "" -> [] through the value converter
    }

    [Fact]
    public async Task Employer_Upsert_Get_Lifecycle()
    {
        using var factory = new ProfilesApiFactory();
        var client = factory.CreateClient();
        var employerId = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/profiles/employers/{employerId}")).StatusCode);

        var create = await client.PutAsJsonAsync(
            $"/profiles/employers/{employerId}",
            TestData.EmployerViewModel(companyName: "Initech"));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var fetched = await client.GetFromJsonAsync<EmployerProfileServiceModel>($"/profiles/employers/{employerId}");
        Assert.Equal(employerId, fetched!.EmployerId);
        Assert.Equal("Initech", fetched.CompanyName);
    }

    [Fact]
    public async Task Employer_Upsert_InvalidWebsite_Returns400()
    {
        using var factory = new ProfilesApiFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/profiles/employers/{Guid.NewGuid()}",
            TestData.EmployerViewModel(website: "not-a-url"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
