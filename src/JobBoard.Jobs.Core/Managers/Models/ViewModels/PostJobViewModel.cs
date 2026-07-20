namespace JobBoard.Jobs.Core.Managers.Models.ViewModels;

/// <summary>
/// Inbound request to post a job — the only shape the post controller binds. The business layer
/// translates it to a <see cref="Domain.Job"/>; it never reaches the database directly.
/// </summary>
public sealed record PostJobViewModel
{
    public string Title { get; init; } = default!;

    public string Description { get; init; } = default!;

    public string Location { get; init; } = default!;

    public SalaryBandViewModel Salary { get; init; } = default!;

    /// <summary>The employer posting the job; reference data owned by Identity, kept locally as a plain Guid.</summary>
    public Guid EmployerId { get; init; }

    public IReadOnlyList<JobClassificationViewModel> Categories { get; init; } = [];

    public IReadOnlyList<JobClassificationViewModel> Tags { get; init; } = [];
}
