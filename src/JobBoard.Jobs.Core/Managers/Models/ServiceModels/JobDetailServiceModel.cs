using JobBoard.Jobs.Core.Managers.Models.Domain;

namespace JobBoard.Jobs.Core.Managers.Models.ServiceModels;

/// <summary>
/// The full job shape returned by <c>GET /jobs/{id}</c>, <c>POST /jobs</c>, and <c>POST /jobs/{id}/close</c>.
/// Maps one-to-one from a loaded <see cref="Job"/> entity; the entity itself never leaves the service.
/// </summary>
public sealed record JobDetailServiceModel(
    Guid Id,
    string Title,
    string Description,
    string Location,
    SalaryBandServiceModel Salary,
    JobStatus Status,
    Guid EmployerId,
    IReadOnlyList<JobClassificationServiceModel> Categories,
    IReadOnlyList<JobClassificationServiceModel> Tags,
    DateTime CreatedOnUtc);
