namespace JobBoard.Profiles.Core.Managers.Models.Domain;

/// <summary>
/// A candidate's résumé profile — the aggregate root of the candidate side of the Profiles context.
/// <see cref="Id"/> <b>is</b> the candidate's account id (sourced from Identity, kept locally as a plain
/// Guid — never a cross-service FK); one profile per candidate.
/// </summary>
public class CandidateProfile
{
    /// <summary>The owning candidate's account id; also this profile's primary key (1:1 with the account).</summary>
    public Guid Id { get; set; }

    public string Headline { get; set; } = default!;

    public string Summary { get; set; } = default!;

    /// <summary>Free-form skills. Persisted as a single newline-delimited column (see the EF configuration).</summary>
    public List<string> Skills { get; set; } = [];

    public string? ResumeUrl { get; set; }

    public DateTime UpdatedOnUtc { get; set; }
}
