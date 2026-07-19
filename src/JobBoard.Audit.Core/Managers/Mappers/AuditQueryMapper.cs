using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Audit.Core.Managers.Models.ViewModels;

namespace JobBoard.Audit.Core.Managers.Mappers;

/// <summary>
/// Translates the inbound <see cref="AuditQueryViewModel"/> into the internal <see cref="AuditQuery"/> the
/// read stack queries by — the ViewModel → Domain step business owns, so nothing below the facade touches an
/// edge type.
/// </summary>
public static class AuditQueryMapper
{
    public static AuditQuery ToAuditQuery(this AuditQueryViewModel viewModel) =>
        new(
            viewModel.CorrelationId,
            viewModel.SubjectId,
            viewModel.ActorId,
            viewModel.FromUtc,
            viewModel.ToUtc);
}
