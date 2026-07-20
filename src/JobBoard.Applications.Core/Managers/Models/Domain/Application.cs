namespace JobBoard.Applications.Core.Managers.Models.Domain;

/// <summary>
/// A candidate's application to a job posting — the aggregate this service owns. <see cref="CandidateId"/>
/// and <see cref="JobId"/> are reference data owned by other services (Identity, Jobs), kept locally as
/// plain <see cref="Guid"/>s, never cross-service foreign keys.
/// </summary>
public class Application
{
    public Guid Id { get; set; }

    /// <summary>The candidate who applied; reference data owned by Identity, kept locally as a plain Guid.</summary>
    public Guid CandidateId { get; set; }

    /// <summary>The posting applied to; reference data owned by Jobs, kept locally as a plain Guid.</summary>
    public Guid JobId { get; set; }

    public ApplicationStatus Status { get; set; }

    /// <summary>Optional pointer to the résumé used (owned by Profiles); free-form reference, not an FK.</summary>
    public string? ResumeReference { get; set; }

    public DateTime SubmittedOnUtc { get; set; }

    public DateTime StatusChangedOnUtc { get; set; }
}
