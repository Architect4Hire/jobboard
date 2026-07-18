using FluentValidation;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Managers.Validators;

/// <summary>
/// Shape/format rules for <see cref="UpsertCandidateProfileViewModel"/> — lengths mirror the EF
/// configuration, each skill must be a single line because <see cref="Domain.CandidateProfile.Skills"/>
/// persists newline-delimited (a newline inside a skill would corrupt the round-trip), and the three
/// professional links must be absolute http(s) URLs when supplied.
/// </summary>
public sealed class UpsertCandidateProfileViewModelValidator : AbstractValidator<UpsertCandidateProfileViewModel>
{
    public const int MaxSkills = 50;
    public const int MaxYearsOfExperience = 70;

    public UpsertCandidateProfileViewModelValidator()
    {
        RuleFor(x => x.Headline).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(4000);

        // Contact & location — optional, length-bounded to match the EF configuration.
        RuleFor(x => x.FullName).MaximumLength(200);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(50);

        // Professional links — optional, but a supplied value must be an absolute http(s) URL.
        HttpUrlRule(x => x.LinkedInUrl, "LinkedIn URL");
        HttpUrlRule(x => x.GitHubUrl, "GitHub URL");
        HttpUrlRule(x => x.PortfolioUrl, "Portfolio URL");

        // Experience & availability.
        RuleFor(x => x.YearsOfExperience)
            .InclusiveBetween(0, MaxYearsOfExperience)
            .When(x => x.YearsOfExperience.HasValue)
            .WithMessage($"'Years Of Experience' must be between 0 and {MaxYearsOfExperience}.");
        RuleFor(x => x.DesiredRole).MaximumLength(200);
        RuleFor(x => x.Availability)
            .IsInEnum()
            .When(x => x.Availability.HasValue)
            .WithMessage("'Availability' is not a recognized value.");

        // A JSON "skills": null overrides the record default and binds null, so guard the collection
        // rules behind the not-null check — otherwise x.Skills.Count would NRE into a 500 instead of a 400.
        RuleFor(x => x.Skills).NotNull();
        When(x => x.Skills is not null, () =>
        {
            RuleFor(x => x.Skills.Count).LessThanOrEqualTo(MaxSkills)
                .WithName("Skills")
                .WithMessage($"'Skills' must contain no more than {MaxSkills} entries.");
            RuleForEach(x => x.Skills)
                .NotEmpty().MaximumLength(100)
                .Must(s => !s.Contains('\n') && !s.Contains('\r'))
                .WithMessage("A skill must be a single line (no line breaks).");
        });
    }

    /// <summary>A shared "optional absolute http(s) URL, ≤2048 chars" rule for the three link fields.</summary>
    private void HttpUrlRule(
        System.Linq.Expressions.Expression<Func<UpsertCandidateProfileViewModel, string?>> selector,
        string displayName)
    {
        var accessor = selector.Compile();
        RuleFor(selector)
            .MaximumLength(2048)
            .Must(BeAnAbsoluteHttpUrl!)
            .When(x => !string.IsNullOrWhiteSpace(accessor(x)))
            .WithName(displayName)
            .WithMessage($"'{displayName}' must be an absolute http(s) URL.");
    }

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
