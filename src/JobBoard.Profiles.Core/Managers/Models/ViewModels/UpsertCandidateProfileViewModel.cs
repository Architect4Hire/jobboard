namespace JobBoard.Profiles.Core.Managers.Models.ViewModels;

/// <summary>
/// Inbound request to create-or-replace a candidate profile — the only shape the candidate upsert
/// controller binds. The owning candidate's id comes from the route, not this body; the business layer
/// translates this to a <see cref="Domain.CandidateProfile"/>.
/// </summary>
public sealed record UpsertCandidateProfileViewModel
{
    public string Headline { get; init; } = default!;

    public string Summary { get; init; } = default!;

    public IReadOnlyList<string> Skills { get; init; } = [];

    public string? ResumeUrl { get; init; }
}
