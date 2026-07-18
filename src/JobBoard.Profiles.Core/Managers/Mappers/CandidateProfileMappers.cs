using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Managers.Mappers;

/// <summary>
/// The two mapping seams the candidate business layer owns: <b>ViewModel → Domain</b> (upsert, taking
/// the owner id from the route) and <b>Domain → ServiceModel</b> (every response). No integration-event
/// mapping — Profiles publishes no events. <see cref="ToEntity"/> deliberately leaves the résumé pointers
/// unset: they're owned by the upload/delete flow, and the business layer carries them across an upsert.
/// </summary>
public static class CandidateProfileMappers
{
    /// <summary>
    /// Translates the upsert request into a <see cref="CandidateProfile"/> keyed by the owning
    /// <paramref name="candidateId"/>, stamping the update time. The data layer decides insert vs update;
    /// the résumé fields are populated by the business layer (preserved from the existing row), not here.
    /// </summary>
    public static CandidateProfile ToEntity(this UpsertCandidateProfileViewModel vm, Guid candidateId) => new()
    {
        Id = candidateId,
        Headline = vm.Headline,
        Summary = vm.Summary,
        Skills = [.. vm.Skills],
        FullName = Trimmed(vm.FullName),
        Location = Trimmed(vm.Location),
        Phone = Trimmed(vm.Phone),
        LinkedInUrl = Trimmed(vm.LinkedInUrl),
        GitHubUrl = Trimmed(vm.GitHubUrl),
        PortfolioUrl = Trimmed(vm.PortfolioUrl),
        YearsOfExperience = vm.YearsOfExperience,
        DesiredRole = Trimmed(vm.DesiredRole),
        Availability = vm.Availability,
        UpdatedOnUtc = DateTime.UtcNow,
    };

    public static CandidateProfileServiceModel ToServiceModel(this CandidateProfile profile) => new(
        profile.Id,
        profile.Headline,
        profile.Summary,
        [.. profile.Skills],
        profile.FullName,
        profile.Location,
        profile.Phone,
        profile.LinkedInUrl,
        profile.GitHubUrl,
        profile.PortfolioUrl,
        profile.YearsOfExperience,
        profile.DesiredRole,
        profile.Availability,
        // The download path is exposed only when a résumé blob is actually present.
        profile.ResumeObjectName is null ? null : $"/profiles/candidates/{profile.Id}/resume",
        profile.ResumeFileName,
        profile.UpdatedOnUtc);

    // Optional free-text fields: normalize "" / whitespace to null so absent and blank are one state.
    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
