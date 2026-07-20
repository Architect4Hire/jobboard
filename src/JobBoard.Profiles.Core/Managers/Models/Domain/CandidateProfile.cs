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

    // --- Contact & location -------------------------------------------------

    /// <summary>The candidate's display name. Optional — a profile can exist before it's fully filled out.</summary>
    public string? FullName { get; set; }

    /// <summary>Free-form location (e.g. "Austin, TX" or "Remote — EU").</summary>
    public string? Location { get; set; }

    public string? Phone { get; set; }

    // --- Professional links -------------------------------------------------

    public string? LinkedInUrl { get; set; }

    public string? GitHubUrl { get; set; }

    public string? PortfolioUrl { get; set; }

    // --- Experience & availability -----------------------------------------

    /// <summary>Total years of professional experience. Optional; when set, a non-negative whole number.</summary>
    public int? YearsOfExperience { get; set; }

    /// <summary>The role/title the candidate is targeting (e.g. "Senior Backend Engineer").</summary>
    public string? DesiredRole { get; set; }

    public CandidateAvailability? Availability { get; set; }

    // --- Résumé file (uploaded to blob storage) -----------------------------

    /// <summary>The résumé blob's object name (key) in storage; <c>null</c> when no résumé is uploaded.
    /// Internal — never leaves the service; the download path is what's exposed on the service model.</summary>
    public string? ResumeObjectName { get; set; }

    /// <summary>The original filename of the uploaded résumé (shown in the UI, used on download).</summary>
    public string? ResumeFileName { get; set; }

    /// <summary>The uploaded résumé's content type, replayed on download so the browser handles it right.</summary>
    public string? ResumeContentType { get; set; }

    public DateTime UpdatedOnUtc { get; set; }
}
