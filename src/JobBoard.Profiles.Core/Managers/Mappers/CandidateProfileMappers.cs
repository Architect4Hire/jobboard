using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Managers.Mappers;

/// <summary>
/// The two mapping seams the candidate business layer owns: <b>ViewModel → Domain</b> (upsert, taking
/// the owner id from the route) and <b>Domain → ServiceModel</b> (every response). No integration-event
/// mapping — Profiles publishes no events.
/// </summary>
public static class CandidateProfileMappers
{
    /// <summary>
    /// Translates the upsert request into a <see cref="CandidateProfile"/> keyed by the owning
    /// <paramref name="candidateId"/>, stamping the update time. The data layer decides insert vs update.
    /// </summary>
    public static CandidateProfile ToEntity(this UpsertCandidateProfileViewModel vm, Guid candidateId) => new()
    {
        Id = candidateId,
        Headline = vm.Headline,
        Summary = vm.Summary,
        Skills = [.. vm.Skills],
        ResumeUrl = vm.ResumeUrl,
        UpdatedOnUtc = DateTime.UtcNow,
    };

    public static CandidateProfileServiceModel ToServiceModel(this CandidateProfile profile) => new(
        profile.Id,
        profile.Headline,
        profile.Summary,
        [.. profile.Skills],
        profile.ResumeUrl,
        profile.UpdatedOnUtc);
}
