using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Core.Managers.Validators;
using Xunit;

namespace JobBoard.Profiles.Tests;

/// <summary>Edge rules for the new candidate metadata: links must be absolute URLs, years must be in range.</summary>
public sealed class CandidateProfileValidatorTests
{
    private readonly UpsertCandidateProfileViewModelValidator _validator = new();

    [Fact]
    public void Valid_Metadata_Passes()
    {
        var result = _validator.Validate(TestData.CandidateViewModel());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void NullLinks_Allowed()
    {
        var result = _validator.Validate(TestData.CandidateViewModel(linkedInUrl: null));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RelativeLink_Fails()
    {
        var result = _validator.Validate(TestData.CandidateViewModel(linkedInUrl: "/in/sam"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("LinkedIn URL"));
    }

    [Fact]
    public void NegativeYears_Fails()
    {
        var result = _validator.Validate(TestData.CandidateViewModel(yearsOfExperience: -1));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void UnrecognizedAvailability_Fails()
    {
        var result = _validator.Validate(TestData.CandidateViewModel(availability: (CandidateAvailability)99));
        Assert.False(result.IsValid);
    }
}
