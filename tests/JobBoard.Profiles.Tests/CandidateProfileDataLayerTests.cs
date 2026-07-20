using JobBoard.Contracts;
using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Mappers;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Tests.Fakes;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Profiles.Tests;

/// <summary>
/// Data-layer behaviour over a real (SQLite) context — the upsert create and update paths both commit
/// through the transaction, alongside the ProfileUpdated outbox row. A fresh context per operation mirrors
/// the request-scoped lifetime.
/// </summary>
public sealed class CandidateProfileDataLayerTests
{
    private static CandidateProfileDataLayer CreateDataLayer(ProfilesDbContext context, IOutbox? outbox = null) =>
        new(new CandidateProfileRepository(context), outbox ?? new Outbox(context));

    private static async Task UpsertAsync(CandidateProfileDataLayer dataLayer, CandidateProfile profile) =>
        await dataLayer.UpsertAsync(profile, profile.ToProfileUpdated(default));

    [Fact]
    public async Task UpsertAsync_CreatesThenUpdates_ThroughTheTransaction_AndEnqueuesProfileUpdated()
    {
        using var harness = new ProfilesSqliteHarness();
        var id = Guid.NewGuid();

        await using (var context = harness.CreateContext())
        {
            await UpsertAsync(CreateDataLayer(context), TestData.CandidateProfile(id: id, headline: "First"));
        }

        await using (var context = harness.CreateContext())
        {
            await UpsertAsync(CreateDataLayer(context), TestData.CandidateProfile(id: id, headline: "Second", skills: ["rust"]));
        }

        await using var assert = harness.CreateContext();
        var profile = Assert.Single(await assert.CandidateProfiles.ToListAsync());
        Assert.Equal("Second", profile.Headline);
        Assert.Equal(["rust"], profile.Skills);

        // Each write shipped a ProfileUpdated row to the ProfileUpdated topic.
        var rows = await assert.OutboxMessages.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(nameof(ProfileUpdated), r.Type));
        Assert.All(rows, r => Assert.Equal(nameof(ProfileUpdated), r.Destination));
    }

    [Fact]
    public async Task UpsertAsync_LeavesNoProfileAndNoOutboxRow_WhenEnqueueThrows()
    {
        using var harness = new ProfilesSqliteHarness();
        var profile = TestData.CandidateProfile(id: Guid.NewGuid(), headline: "Rolled back");

        await using (var context = harness.CreateContext())
        {
            var dataLayer = CreateDataLayer(context, new FakeOutbox { ThrowOnEnqueue = true });
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dataLayer.UpsertAsync(profile, profile.ToProfileUpdated(default)));
        }

        await using var assert = harness.CreateContext();
        Assert.Empty(await assert.CandidateProfiles.ToListAsync());
        Assert.Empty(await assert.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task UpsertAsync_DuplicateKeyViolation_ThrowsRetryableConflict()
    {
        // The repository's upsert trips a primary-key violation (concurrent first insert); the data layer
        // maps it to a 409 rather than letting a raw DbUpdateException become a 500.
        var profile = TestData.CandidateProfile();
        var dataLayer = new CandidateProfileDataLayer(new FakeDuplicateKeyCandidateProfileRepository(), new FakeOutbox());

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => dataLayer.UpsertAsync(profile, profile.ToProfileUpdated(default)));

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
            await UpsertAsync(CreateDataLayer(context), TestData.CandidateProfile(id: id, headline: "Reader"));
        }

        await using var read = harness.CreateContext();
        var found = await CreateDataLayer(read).GetAsync(id);

        Assert.NotNull(found);
        Assert.Equal("Reader", found!.Headline);
    }
}
