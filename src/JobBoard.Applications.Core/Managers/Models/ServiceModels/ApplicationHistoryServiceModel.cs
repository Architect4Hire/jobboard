using JobBoard.Applications.Core.Managers.Models.Domain;

namespace JobBoard.Applications.Core.Managers.Models.ServiceModels;

/// <summary>
/// The list-row shape returned by <c>GET /applications/mine</c> — a candidate's applications enriched with
/// the job title and employer name mirrored locally from <see cref="JobReference"/> and
/// <see cref="EmployerReference"/> (ADR-0012 option B: a materialized read-model projection fed by
/// events). Assembled entirely from this service's own database; no cross-service call on the read path.
/// </summary>
public sealed record ApplicationHistoryServiceModel(
    Guid Id,
    Guid JobId,
    string JobTitle,
    Guid EmployerId,
    string EmployerName,
    ApplicationStatus Status,
    DateTime SubmittedOnUtc,
    DateTime StatusChangedOnUtc);
