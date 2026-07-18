using JobBoard.Profiles.Core.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Profiles.Tests;

public sealed class EmployerProfileDataLayerTests
{
    private static EmployerProfileDataLayer CreateDataLayer(ProfilesDbContext context) =>
        new(new EmployerProfileRepository(context));

    [Fact]
    public async Task UpsertAsync_CreatesThenUpdates_ThroughTheTransaction()
    {
        using var harness = new ProfilesSqliteHarness();
        var id = Guid.NewGuid();

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).UpsertAsync(TestData.EmployerProfile(id: id, companyName: "First"));
        }

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).UpsertAsync(TestData.EmployerProfile(id: id, companyName: "Second"));
        }

        await using var assert = harness.CreateContext();
        var profile = Assert.Single(await assert.EmployerProfiles.ToListAsync());
        Assert.Equal("Second", profile.CompanyName);
    }
}
