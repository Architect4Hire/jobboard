using FluentValidation;
using JobBoard.Applications.Core.Managers.Models.ViewModels;

namespace JobBoard.Applications.Core.Managers.Validators;

/// <summary>
/// Shape rule for <see cref="AdvanceApplicationStatusViewModel"/>: the target must be a defined
/// <c>ApplicationStatus</c>. Whether the transition is <i>legal from the current status</i> is a
/// data-dependent domain rule, decided in business against the loaded application.
/// </summary>
public sealed class AdvanceApplicationStatusViewModelValidator : AbstractValidator<AdvanceApplicationStatusViewModel>
{
    public AdvanceApplicationStatusViewModelValidator()
    {
        RuleFor(x => x.TargetStatus).IsInEnum();
    }
}
