using JobBoard.Profiles.Core.Managers.Models.Domain;

namespace JobBoard.Profiles.Core.Managers.Models.ServiceModels;

/// <summary>
/// The candidate profile shape returned by <c>GET</c>/<c>PUT /profiles/candidates/{candidateId}</c>.
/// Maps one-to-one from a loaded <see cref="Domain.CandidateProfile"/>; the entity never leaves the service.
/// <see cref="ResumeUrl"/> is the gateway-relative <b>download path</b> for the uploaded résumé (populated
/// only when one exists), not a stored value — the bytes are served by the résumé download endpoint.
/// </summary>
public sealed record CandidateProfileServiceModel(
    Guid CandidateId,
    string Headline,
    string Summary,
    IReadOnlyList<string> Skills,
    string? FullName,
    string? Location,
    string? Phone,
    string? LinkedInUrl,
    string? GitHubUrl,
    string? PortfolioUrl,
    int? YearsOfExperience,
    string? DesiredRole,
    CandidateAvailability? Availability,
    string? ResumeUrl,
    string? ResumeFileName,
    DateTime UpdatedOnUtc);
