using JobBoard.Profiles.Core.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Profiles.Tests;

public sealed class CandidateProfileRepositoryTests
{
    [Fact]
    public async Task GetAsync_ReturnsProfile_OrNull()
    {
        using var harness = new ProfilesSqliteHarness();
        var profile = TestData.CandidateProfile();

        await using (var seed = harness.CreateContext())
        {
            seed.CandidateProfiles.Add(profile);
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new CandidateProfileRepository(context);

        Assert.Equal(profile.Id, (await repository.GetAsync(profile.Id))!.Id);
        Assert.Null(await repository.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpsertAsync_Inserts_ThenUpdates_WithSkillsRoundTrip()
    {
        using var harness = new ProfilesSqliteHarness();
        var id = Guid.NewGuid();

        // Insert.
        await using (var context = harness.CreateContext())
        {
            var repository = new CandidateProfileRepository(context);
            await repository.UpsertAsync(TestData.CandidateProfile(id: id, headline: "Junior Dev", skills: ["c#", "sql"]));
            await context.SaveChangesAsync();
        }

        await using (var assert = harness.CreateContext())
        {
            var inserted = await assert.CandidateProfiles.SingleAsync();
            Assert.Equal("Junior Dev", inserted.Headline);
            Assert.Equal(["c#", "sql"], inserted.Skills); // survived the newline-delimited value converter
        }

        // Update the same owner id — new headline + different skills.
        await using (var context = harness.CreateContext())
        {
            var repository = new CandidateProfileRepository(context);
            await repository.UpsertAsync(TestData.CandidateProfile(id: id, headline: "Senior Dev", skills: ["go", "rust", "k8s"]));
            await context.SaveChangesAsync();
        }

        await using (var assert = harness.CreateContext())
        {
            var updated = Assert.Single(await assert.CandidateProfiles.ToListAsync()); // still one row, not two
            Assert.Equal("Senior Dev", updated.Headline);
            Assert.Equal(["go", "rust", "k8s"], updated.Skills);
        }
    }

    [Fact]
    public async Task IsDuplicateKeyViolation_TrueForRealPrimaryKeyViolation()
    {
        using var harness = new ProfilesSqliteHarness();
        var id = Guid.NewGuid();

        await using (var seed = harness.CreateContext())
        {
            seed.CandidateProfiles.Add(TestData.CandidateProfile(id: id));
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        context.CandidateProfiles.Add(TestData.CandidateProfile(id: id)); // same primary key

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.True(CandidateProfileRepository.IsDuplicateKeyViolation(ex));
    }

    [Fact]
    public void IsDuplicateKeyViolation_FalseForUnrelatedFailure()
    {
        var unrelated = new DbUpdateException("boom", new Exception("connection reset"));
        Assert.False(CandidateProfileRepository.IsDuplicateKeyViolation(unrelated));
    }
}
