using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Tests.Fakes;
using Xunit;

namespace JobBoard.Profiles.Tests;

public sealed class CandidateProfileBusinessTests
{
    [Fact]
    public async Task UpsertAsync_TranslatesViewModel_WithRouteOwnerId_AndMapsResult()
    {
        var dataLayer = new FakeCandidateProfileDataLayer();
        var business = new CandidateProfileBusiness(dataLayer);
        var candidateId = Guid.NewGuid();

        var result = await business.UpsertAsync(
            candidateId,
            TestData.CandidateViewModel(headline: "Staff Engineer", skills: ["c#", "azure"]));

        // The owner id came from the route, not the body; fields translated onto the entity.
        Assert.NotNull(dataLayer.Upserted);
        Assert.Equal(candidateId, dataLayer.Upserted!.Id);
        Assert.Equal("Staff Engineer", dataLayer.Upserted.Headline);
        Assert.Equal(["c#", "azure"], dataLayer.Upserted.Skills);

        // The response mirrors the persisted entity.
        Assert.Equal(candidateId, result.CandidateId);
        Assert.Equal("Staff Engineer", result.Headline);
        Assert.Equal(["c#", "azure"], result.Skills);
    }

    [Fact]
    public async Task GetAsync_MapsEntity_OrReturnsNull()
    {
        var candidateId = Guid.NewGuid();
        var dataLayer = new FakeCandidateProfileDataLayer { GetResult = TestData.CandidateProfile(id: candidateId, headline: "Mapped") };
        var business = new CandidateProfileBusiness(dataLayer);

        var found = await business.GetAsync(candidateId);
        Assert.Equal("Mapped", found!.Headline);
        Assert.Equal(candidateId, found.CandidateId);

        var missingDataLayer = new FakeCandidateProfileDataLayer { GetResult = null };
        Assert.Null(await new CandidateProfileBusiness(missingDataLayer).GetAsync(Guid.NewGuid()));
    }
}
