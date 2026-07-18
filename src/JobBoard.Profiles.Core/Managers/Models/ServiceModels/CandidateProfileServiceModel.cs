namespace JobBoard.Profiles.Core.Managers.Models.ServiceModels;

/// <summary>
/// The candidate profile shape returned by <c>GET</c>/<c>PUT /profiles/candidates/{candidateId}</c>.
/// Maps one-to-one from a loaded <see cref="Domain.CandidateProfile"/>; the entity never leaves the service.
/// </summary>
public sealed record CandidateProfileServiceModel(
    Guid CandidateId,
    string Headline,
    string Summary,
    IReadOnlyList<string> Skills,
    string? ResumeUrl,
    DateTime UpdatedOnUtc);
