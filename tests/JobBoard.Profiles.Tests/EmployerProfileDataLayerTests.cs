using JobBoard.Contracts;
using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Mappers;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Tests.Fakes;
using JobBoard.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Profiles.Tests;

public sealed class EmployerProfileDataLayerTests
{
    private static EmployerProfileDataLayer CreateDataLayer(ProfilesDbContext context, IOutbox? outbox = null) =>
        new(new EmployerProfileRepository(context), outbox ?? new Outbox(context));

    private static async Task UpsertAsync(EmployerProfileDataLayer dataLayer, EmployerProfile profile) =>
        await dataLayer.UpsertAsync(profile, profile.ToProfileUpdated(default));

    [Fact]
    public async Task UpsertAsync_CreatesThenUpdates_ThroughTheTransaction_AndEnqueuesProfileUpdated()
    {
        using var harness = new ProfilesSqliteHarness();
        var id = Guid.NewGuid();

        await using (var context = harness.CreateContext())
        {
            await UpsertAsync(CreateDataLayer(context), TestData.EmployerProfile(id: id, companyName: "First"));
        }

        await using (var context = harness.CreateContext())
        {
            await UpsertAsync(CreateDataLayer(context), TestData.EmployerProfile(id: id, companyName: "Second"));
        }

        await using var assert = harness.CreateContext();
        var profile = Assert.Single(await assert.EmployerProfiles.ToListAsync());
        Assert.Equal("Second", profile.CompanyName);

        var rows = await assert.OutboxMessages.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(nameof(ProfileUpdated), r.Type));
    }

    [Fact]
    public async Task UpsertAsync_LeavesNoProfileAndNoOutboxRow_WhenEnqueueThrows()
    {
        using var harness = new ProfilesSqliteHarness();
        var profile = TestData.EmployerProfile(id: Guid.NewGuid(), companyName: "Rolled back");

        await using (var context = harness.CreateContext())
        {
            var dataLayer = CreateDataLayer(context, new FakeOutbox { ThrowOnEnqueue = true });
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dataLayer.UpsertAsync(profile, profile.ToProfileUpdated(default)));
        }

        await using var assert = harness.CreateContext();
        Assert.Empty(await assert.EmployerProfiles.ToListAsync());
        Assert.Empty(await assert.OutboxMessages.ToListAsync());
    }
}
