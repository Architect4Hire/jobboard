using JobBoard.Jobs.Core.Managers.Models.Domain;

namespace JobBoard.Jobs.Core.Managers.Models.ServiceModels;

/// <summary>
/// The list-row shape returned by <c>GET /jobs</c>. Projected straight from the entity in SQL by the
/// repository — deliberately lighter than <see cref="JobDetailServiceModel"/> (no description, no tags,
/// category slugs only) so a list query never loads full graphs.
/// </summary>
public sealed record JobSummaryServiceModel(
    Guid Id,
    string Title,
    string Location,
    SalaryBandServiceModel Salary,
    JobStatus Status,
    IReadOnlyList<string> CategorySlugs,
    DateTime CreatedOnUtc);
