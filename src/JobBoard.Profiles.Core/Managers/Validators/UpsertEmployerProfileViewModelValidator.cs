using FluentValidation;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Managers.Validators;

/// <summary>
/// Shape/format rules for <see cref="UpsertEmployerProfileViewModel"/> — lengths mirror the EF
/// configuration; the optional website, when present, must be an absolute http(s) URL.
/// </summary>
public sealed class UpsertEmployerProfileViewModelValidator : AbstractValidator<UpsertEmployerProfileViewModel>
{
    public UpsertEmployerProfileViewModelValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Website).MaximumLength(2048).Must(BeAnAbsoluteHttpUrl!)
            .When(x => !string.IsNullOrWhiteSpace(x.Website))
            .WithMessage("'Website' must be an absolute http(s) URL.");
    }

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
