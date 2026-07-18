using FluentValidation;
using JobBoard.Identity.Core.Managers.Models.ViewModels;

namespace JobBoard.Identity.Core.Managers.Validators;

/// <summary>
/// Shape/format rules for <see cref="RegisterAccountViewModel"/> — a valid email, a password that
/// clears the minimum length, and a role that is one of the defined enum values. Credential checks that
/// need the database (is this email already taken?) are <b>not</b> here; the data layer enforces
/// uniqueness and maps a collision to a conflict.
/// </summary>
public sealed class RegisterAccountViewModelValidator : AbstractValidator<RegisterAccountViewModel>
{
    public const int MinimumPasswordLength = 8;

    public RegisterAccountViewModelValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(MinimumPasswordLength).MaximumLength(128);
        RuleFor(x => x.Role).IsInEnum();
    }
}
