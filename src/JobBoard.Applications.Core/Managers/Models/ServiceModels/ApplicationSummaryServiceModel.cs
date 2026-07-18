using JobBoard.Applications.Core.Managers.Models.Domain;

namespace JobBoard.Applications.Core.Managers.Models.ServiceModels;

/// <summary>
/// The list-row shape returned by <c>GET /applications?candidateId=…</c>. Projected straight from the
/// entity in SQL by the repository — deliberately lighter than <see cref="ApplicationDetailServiceModel"/>
/// (no résumé reference) so a list query never loads full entities.
/// </summary>
public sealed record ApplicationSummaryServiceModel(
    Guid Id,
    Guid JobId,
    ApplicationStatus Status,
    DateTime SubmittedOnUtc,
    DateTime StatusChangedOnUtc);
