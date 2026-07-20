using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Tests.Fakes;
using JobBoard.Shared.Requests;
using Xunit;

namespace JobBoard.Profiles.Tests;

public sealed class EmployerProfileBusinessTests
{
    private static readonly Guid CorrelationId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly IRequestContext RequestContext = BuildContext();

    private static AmbientRequestContext BuildContext()
    {
        var context = new AmbientRequestContext();
        context.Populate(CorrelationId, ActorId, "employer");
        return context;
    }

    private static EmployerProfileBusiness Create(FakeEmployerProfileDataLayer dataLayer) =>
        new(dataLayer, RequestContext);

    [Fact]
    public async Task UpsertAsync_TranslatesViewModel_WithRouteOwnerId_AndMapsResult()
    {
        var dataLayer = new FakeEmployerProfileDataLayer();
        var business = Create(dataLayer);
        var employerId = Guid.NewGuid();

        var result = await business.UpsertAsync(
            employerId,
            TestData.EmployerViewModel(companyName: "Globex", website: "https://globex.example.com"));

        Assert.NotNull(dataLayer.Upserted);
        Assert.Equal(employerId, dataLayer.Upserted!.Id);
        Assert.Equal("Globex", dataLayer.Upserted.CompanyName);

        Assert.Equal(employerId, result.EmployerId);
        Assert.Equal("Globex", result.CompanyName);
        Assert.Equal("https://globex.example.com", result.Website);

        // The upsert emits ProfileUpdated with the "Employer" discriminator and the request thread.
        var updated = dataLayer.UpdatedEvent;
        Assert.NotNull(updated);
        Assert.NotEqual(Guid.Empty, updated!.Id);
        Assert.Equal(employerId, updated.ProfileId);
        Assert.Equal("Employer", updated.ProfileType);
        Assert.Equal(CorrelationId, updated.CorrelationId);
        Assert.Equal(CorrelationId, updated.CausationId);
        Assert.Equal(ActorId, updated.ActorId);
    }

    [Fact]
    public async Task GetAsync_MapsEntity_OrReturnsNull()
    {
        var employerId = Guid.NewGuid();
        var dataLayer = new FakeEmployerProfileDataLayer { GetResult = TestData.EmployerProfile(id: employerId, companyName: "Mapped Co") };
        var business = Create(dataLayer);

        var found = await business.GetAsync(employerId);
        Assert.Equal("Mapped Co", found!.CompanyName);
        Assert.Equal(employerId, found.EmployerId);

        var missingDataLayer = new FakeEmployerProfileDataLayer { GetResult = null };
        Assert.Null(await Create(missingDataLayer).GetAsync(Guid.NewGuid()));
    }
}
