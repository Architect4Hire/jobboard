using FluentValidation;
using JobBoard.Profiles.Core.Facade;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Validators;
using JobBoard.Profiles.Tests.Fakes;
using Xunit;

namespace JobBoard.Profiles.Tests;

/// <summary>Facade validation seam for both aggregates: a bad view model short-circuits before business.</summary>
public sealed class ProfileFacadeTests
{
    private static readonly CandidateProfileServiceModel AnyCandidate =
        new(Guid.NewGuid(), "Engineer", "Summary", ["c#"], null, DateTime.UtcNow);

    private static readonly EmployerProfileServiceModel AnyEmployer =
        new(Guid.NewGuid(), "Acme", null, "Desc", DateTime.UtcNow);

    private static CandidateProfileFacade CreateCandidateFacade(FakeCandidateProfileBusiness business) =>
        new(business, new UpsertCandidateProfileViewModelValidator());

    private static EmployerProfileFacade CreateEmployerFacade(FakeEmployerProfileBusiness business) =>
        new(business, new UpsertEmployerProfileViewModelValidator());

    [Fact]
    public async Task Candidate_UpsertAsync_Valid_DelegatesToBusiness()
    {
        var business = new FakeCandidateProfileBusiness { UpsertResult = AnyCandidate };
        var facade = CreateCandidateFacade(business);

        await facade.UpsertAsync(Guid.NewGuid(), TestData.CandidateViewModel());

        Assert.Equal(1, business.UpsertCallCount);
    }

    [Fact]
    public async Task Candidate_UpsertAsync_EmptyHeadline_Throws_AndNeverReachesBusiness()
    {
        var business = new FakeCandidateProfileBusiness { UpsertResult = AnyCandidate };
        var facade = CreateCandidateFacade(business);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.UpsertAsync(Guid.NewGuid(), TestData.CandidateViewModel(headline: "")));
        Assert.Equal(0, business.UpsertCallCount);
    }

    [Fact]
    public async Task Employer_UpsertAsync_Valid_DelegatesToBusiness()
    {
        var business = new FakeEmployerProfileBusiness { UpsertResult = AnyEmployer };
        var facade = CreateEmployerFacade(business);

        await facade.UpsertAsync(Guid.NewGuid(), TestData.EmployerViewModel());

        Assert.Equal(1, business.UpsertCallCount);
    }

    [Fact]
    public async Task Employer_UpsertAsync_InvalidWebsite_Throws_AndNeverReachesBusiness()
    {
        var business = new FakeEmployerProfileBusiness { UpsertResult = AnyEmployer };
        var facade = CreateEmployerFacade(business);

        await Assert.ThrowsAsync<ValidationException>(
            () => facade.UpsertAsync(Guid.NewGuid(), TestData.EmployerViewModel(website: "not-a-url")));
        Assert.Equal(0, business.UpsertCallCount);
    }
}
