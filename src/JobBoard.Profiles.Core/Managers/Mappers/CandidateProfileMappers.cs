using JobBoard.Contracts;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using JobBoard.Shared.Requests;

namespace JobBoard.Profiles.Core.Managers.Mappers;

/// <summary>
/// The mapping seams the candidate business layer owns: <b>ViewModel → Domain</b> (upsert, taking the owner
/// id from the route), <b>Domain → ServiceModel</b> (every response), and <b>Domain → integration event</b>
/// (the <see cref="ProfileUpdated"/> audit fact). <see cref="ToEntity"/> deliberately leaves the résumé
/// pointers unset: they're owned by the upload/delete flow, and the business layer carries them across an
/// upsert. The event carries ids + type + timestamp only — never the profile's field values.
/// </summary>
public static class CandidateProfileMappers
{
    /// <summary>
    /// Builds the <see cref="ProfileUpdated"/> fact for a candidate profile that has just been written
    /// (edited or its résumé changed), stamping a fresh event id and the audit <paramref name="thread"/>
    /// (ADR-0013). The subject is the profile id (== the owning candidate's account id) and the occurred-at
    /// is the row's update time; no résumé PII is included.
    /// </summary>
    public static ProfileUpdated ToProfileUpdated(this CandidateProfile profile, AuditThread thread) =>
        new(Guid.NewGuid(), profile.Id, "Candidate", profile.UpdatedOnUtc)
        {
            CorrelationId = thread.CorrelationId,
            CausationId = thread.CausationId,
            ActorId = thread.ActorId,
        };

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
