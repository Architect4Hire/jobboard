using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Tests;

/// <summary>Builders for the fixtures the Profiles tests share, kept terse and override-friendly.</summary>
internal static class TestData
{
    public static CandidateProfile CandidateProfile(
        Guid? id = null,
        string headline = "Senior Engineer",
        IEnumerable<string>? skills = null,
        string? resumeObjectName = null,
        string? resumeFileName = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Headline = headline,
        Summary = "Ten years building things.",
        Skills = skills?.ToList() ?? ["c#", "sql"],
        FullName = "Sam Example",
        Location = "Remote",
        LinkedInUrl = "https://linkedin.com/in/sam",
        YearsOfExperience = 10,
        DesiredRole = "Staff Engineer",
        Availability = CandidateAvailability.Immediate,
        ResumeObjectName = resumeObjectName,
        ResumeFileName = resumeFileName,
        ResumeContentType = resumeObjectName is null ? null : "application/pdf",
        UpdatedOnUtc = DateTime.UtcNow,
    };

    public static EmployerProfile EmployerProfile(
        Guid? id = null,
        string companyName = "Acme Corp",
        string? website = "https://acme.example.com") => new()
    {
        Id = id ?? Guid.NewGuid(),
        CompanyName = companyName,
        Website = website,
        Description = "We make everything.",
        UpdatedOnUtc = DateTime.UtcNow,
    };

    public static UpsertCandidateProfileViewModel CandidateViewModel(
        string headline = "Senior Engineer",
        IReadOnlyList<string>? skills = null,
        string? linkedInUrl = "https://linkedin.com/in/sam",
        int? yearsOfExperience = 10,
        CandidateAvailability? availability = CandidateAvailability.Immediate) => new()
    {
        Headline = headline,
        Summary = "Ten years building things.",
        Skills = skills ?? ["c#", "sql"],
        FullName = "Sam Example",
        Location = "Remote",
        LinkedInUrl = linkedInUrl,
        YearsOfExperience = yearsOfExperience,
        DesiredRole = "Staff Engineer",
        Availability = availability,
    };

    public static UpsertEmployerProfileViewModel EmployerViewModel(
        string companyName = "Acme Corp",
        string? website = "https://acme.example.com") => new()
    {
        CompanyName = companyName,
        Website = website,
        Description = "We make everything.",
    };
}
