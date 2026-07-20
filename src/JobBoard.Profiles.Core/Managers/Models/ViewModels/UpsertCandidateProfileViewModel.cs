using JobBoard.Profiles.Core.Managers.Models.Domain;

namespace JobBoard.Profiles.Core.Managers.Models.ViewModels;

/// <summary>
/// Inbound request to create-or-replace a candidate profile — the only shape the candidate upsert
/// controller binds. The owning candidate's id comes from the route, not this body; the business layer
/// translates this to a <see cref="Domain.CandidateProfile"/>. The résumé file is <b>not</b> here — it's
/// managed by the dedicated upload/download/delete endpoints, and a profile save never disturbs it.
/// </summary>
public sealed record UpsertCandidateProfileViewModel
{
    public string Headline { get; init; } = default!;

    public string Summary { get; init; } = default!;

    public IReadOnlyList<string> Skills { get; init; } = [];

    // Contact & location.
    public string? FullName { get; init; }

    public string? Location { get; init; }

    public string? Phone { get; init; }

    // Professional links.
    public string? LinkedInUrl { get; init; }

    public string? GitHubUrl { get; init; }

    public string? PortfolioUrl { get; init; }

    // Experience & availability.
    public int? YearsOfExperience { get; init; }

    public string? DesiredRole { get; init; }

    public CandidateAvailability? Availability { get; init; }
}
