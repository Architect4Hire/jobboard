using FluentValidation;
using JobBoard.Audit.Core.Managers.Models.ViewModels;

namespace JobBoard.Audit.Core.Managers.Validators;

/// <summary>
/// Validates the support-query filter at the edge (SCRUB A6). Two rules: at least one filter must be
/// supplied — an all-empty query would scan the whole trail, which the read surface must not allow — and a
/// time window must not be inverted. On failure the shared exception handler returns the shared error shape,
/// not a raw exception.
/// </summary>
public sealed class AuditQueryViewModelValidator : AbstractValidator<AuditQueryViewModel>
{
    public AuditQueryViewModelValidator()
    {
        RuleFor(query => query)
            .Must(HasAtLeastOneFilter)
            .WithMessage(
                "Provide at least one filter: correlationId, subjectId, actorId, or a from/to time window.");

        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue)
            .WithMessage("'toUtc' must be at or after 'fromUtc'.");
    }

    private static bool HasAtLeastOneFilter(AuditQueryViewModel query) =>
        query.CorrelationId.HasValue
        || query.SubjectId.HasValue
        || query.ActorId.HasValue
        || query.FromUtc.HasValue
        || query.ToUtc.HasValue;
}
