using FluentValidation;
using JobBoard.Identity.Core.Managers.Models.ViewModels;

namespace JobBoard.Identity.Core.Managers.Validators;

/// <summary>
/// Shape rules for <see cref="LoginViewModel"/> — both fields present. Whether the credentials are
/// <i>correct</i> is a data-dependent check (load the account, verify the hash) and lives in business,
/// which returns the same 401 for a bad email or a bad password so neither is revealed.
/// </summary>
public sealed class LoginViewModelValidator : AbstractValidator<LoginViewModel>
{
    public LoginViewModelValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
