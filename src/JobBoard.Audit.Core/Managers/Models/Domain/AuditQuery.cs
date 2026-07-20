namespace JobBoard.Audit.Core.Managers.Models.Domain;

/// <summary>
/// The internal filter the data layer and repository query by — the validated
/// <see cref="ViewModels.AuditQueryViewModel"/> translated into the domain shape (business owns that
/// translation). Each populated field narrows the trail; an omitted one is not applied. Kept separate from
/// the ViewModel so the read stack below the facade never binds to an inbound edge type.
/// </summary>
public sealed record AuditQuery(
    Guid? CorrelationId,
    Guid? SubjectId,
    Guid? ActorId,
    DateTime? FromUtc,
    DateTime? ToUtc);
