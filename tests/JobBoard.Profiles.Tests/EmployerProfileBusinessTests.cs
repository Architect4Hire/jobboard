using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Tests.Fakes;
using Xunit;

namespace JobBoard.Profiles.Tests;

public sealed class EmployerProfileBusinessTests
{
    [Fact]
    public async Task UpsertAsync_TranslatesViewModel_WithRouteOwnerId_AndMapsResult()
    {
        var dataLayer = new FakeEmployerProfileDataLayer();
        var business = new EmployerProfileBusiness(dataLayer);
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
    }

    [Fact]
    public async Task GetAsync_MapsEntity_OrReturnsNull()
    {
        var employerId = Guid.NewGuid();
        var dataLayer = new FakeEmployerProfileDataLayer { GetResult = TestData.EmployerProfile(id: employerId, companyName: "Mapped Co") };
        var business = new EmployerProfileBusiness(dataLayer);

        var found = await business.GetAsync(employerId);
        Assert.Equal("Mapped Co", found!.CompanyName);
        Assert.Equal(employerId, found.EmployerId);

        var missingDataLayer = new FakeEmployerProfileDataLayer { GetResult = null };
        Assert.Null(await new EmployerProfileBusiness(missingDataLayer).GetAsync(Guid.NewGuid()));
    }
}
