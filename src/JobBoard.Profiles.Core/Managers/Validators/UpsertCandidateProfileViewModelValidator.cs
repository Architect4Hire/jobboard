using FluentValidation;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Managers.Validators;

/// <summary>
/// Shape/format rules for <see cref="UpsertCandidateProfileViewModel"/> — lengths mirror the EF
/// configuration, and each skill must be a single line because <see cref="Domain.CandidateProfile.Skills"/>
/// persists newline-delimited (a newline inside a skill would corrupt the round-trip).
/// </summary>
public sealed class UpsertCandidateProfileViewModelValidator : AbstractValidator<UpsertCandidateProfileViewModel>
{
    public const int MaxSkills = 50;

    public UpsertCandidateProfileViewModelValidator()
    {
        RuleFor(x => x.Headline).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ResumeUrl).MaximumLength(2048).Must(BeAnAbsoluteHttpUrl!)
            .When(x => !string.IsNullOrWhiteSpace(x.ResumeUrl))
            .WithMessage("'Resume Url' must be an absolute http(s) URL.");

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

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
