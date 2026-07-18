using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Tests.Fakes;
using JobBoard.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Profiles.Tests;

/// <summary>
/// Data-layer behaviour over a real (SQLite) context — the upsert create and update paths both commit
/// through the transaction. A fresh context per operation mirrors the request-scoped lifetime.
/// </summary>
public sealed class CandidateProfileDataLayerTests
{
    private static CandidateProfileDataLayer CreateDataLayer(ProfilesDbContext context) =>
        new(new CandidateProfileRepository(context));

    [Fact]
    public async Task UpsertAsync_CreatesThenUpdates_ThroughTheTransaction()
    {
        using var harness = new ProfilesSqliteHarness();
        var id = Guid.NewGuid();

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).UpsertAsync(TestData.CandidateProfile(id: id, headline: "First"));
        }

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).UpsertAsync(TestData.CandidateProfile(id: id, headline: "Second", skills: ["rust"]));
        }

        await using var assert = harness.CreateContext();
        var profile = Assert.Single(await assert.CandidateProfiles.ToListAsync());
        Assert.Equal("Second", profile.Headline);
        Assert.Equal(["rust"], profile.Skills);
    }

    [Fact]
    public async Task UpsertAsync_DuplicateKeyViolation_ThrowsRetryableConflict()
    {
        // The repository's upsert trips a primary-key violation (concurrent first insert); the data layer
        // maps it to a 409 rather than letting a raw DbUpdateException become a 500.
        var dataLayer = new CandidateProfileDataLayer(new FakeDuplicateKeyCandidateProfileRepository());

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => dataLayer.UpsertAsync(TestData.CandidateProfile()));

        Assert.Equal("candidate_profile.conflict", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task GetAsync_PassesThroughToRepository()
    {
        using var harness = new ProfilesSqliteHarness();
        var id = Guid.NewGuid();

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).UpsertAsync(TestData.CandidateProfile(id: id, headline: "Reader"));
        }

        await using var read = harness.CreateContext();
        var found = await CreateDataLayer(read).GetAsync(id);

        Assert.NotNull(found);
        Assert.Equal("Reader", found!.Headline);
    }
}
