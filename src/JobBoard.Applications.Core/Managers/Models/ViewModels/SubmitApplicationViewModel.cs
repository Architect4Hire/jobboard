namespace JobBoard.Applications.Core.Managers.Models.ViewModels;

/// <summary>
/// Inbound request to submit an application — the only shape the submit controller binds. The business
/// layer translates it to a <see cref="Domain.Application"/> in the <c>Submitted</c> state; it never
/// reaches the database directly.
/// </summary>
public sealed record SubmitApplicationViewModel
{
    /// <summary>The candidate applying; reference data owned by Identity, kept locally as a plain Guid.</summary>
    public Guid CandidateId { get; init; }

    /// <summary>The posting being applied to; reference data owned by Jobs, kept locally as a plain Guid.</summary>
    public Guid JobId { get; init; }

    /// <summary>Optional pointer to the résumé used (owned by Profiles); free-form reference, not an FK.</summary>
    public string? ResumeReference { get; init; }
}
