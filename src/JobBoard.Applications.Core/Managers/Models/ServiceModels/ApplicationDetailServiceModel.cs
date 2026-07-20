using JobBoard.Applications.Core.Managers.Models.Domain;

namespace JobBoard.Applications.Core.Managers.Models.ServiceModels;

/// <summary>
/// The full application shape returned by <c>GET /applications/{id}</c>, <c>POST /applications</c>,
/// <c>POST /applications/{id}/withdraw</c>, and <c>POST /applications/{id}/advance</c>. Maps one-to-one
/// from a loaded <see cref="Application"/> entity; the entity itself never leaves the service.
/// </summary>
public sealed record ApplicationDetailServiceModel(
    Guid Id,
    Guid CandidateId,
    Guid JobId,
    ApplicationStatus Status,
    string? ResumeReference,
    DateTime SubmittedOnUtc,
    DateTime StatusChangedOnUtc);
