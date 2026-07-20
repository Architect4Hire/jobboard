using FluentValidation;
using JobBoard.Applications.Core.Managers.Models.ViewModels;

namespace JobBoard.Applications.Core.Managers.Validators;

/// <summary>
/// Shape/format rules for <see cref="SubmitApplicationViewModel"/>. Data-dependent rules — "the candidate
/// hasn't already applied to this job" — are enforced in the write (a unique index + conflict mapping),
/// not here.
/// </summary>
public sealed class SubmitApplicationViewModelValidator : AbstractValidator<SubmitApplicationViewModel>
{
    public SubmitApplicationViewModelValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.ResumeReference).MaximumLength(2048)
            .When(x => x.ResumeReference is not null);
    }
}
