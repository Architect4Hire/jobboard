using JobBoard.Profiles.Core.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Profiles.Tests;

public sealed class EmployerProfileRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_Inserts_ThenUpdates_SingleRow()
    {
        using var harness = new ProfilesSqliteHarness();
        var id = Guid.NewGuid();

        await using (var context = harness.CreateContext())
        {
            var repository = new EmployerProfileRepository(context);
            await repository.UpsertAsync(TestData.EmployerProfile(id: id, companyName: "Acme"));
            await context.SaveChangesAsync();
        }

        await using (var context = harness.CreateContext())
        {
            var repository = new EmployerProfileRepository(context);
            await repository.UpsertAsync(TestData.EmployerProfile(id: id, companyName: "Acme Renamed", website: null));
            await context.SaveChangesAsync();
        }

        await using var assert = harness.CreateContext();
        var updated = Assert.Single(await assert.EmployerProfiles.ToListAsync());
        Assert.Equal("Acme Renamed", updated.CompanyName);
        Assert.Null(updated.Website);
    }

    [Fact]
    public async Task GetAsync_ReturnsProfile_OrNull()
    {
        using var harness = new ProfilesSqliteHarness();
        var profile = TestData.EmployerProfile();

        await using (var seed = harness.CreateContext())
        {
            seed.EmployerProfiles.Add(profile);
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new EmployerProfileRepository(context);

        Assert.Equal(profile.Id, (await repository.GetAsync(profile.Id))!.Id);
        Assert.Null(await repository.GetAsync(Guid.NewGuid()));
    }
}
