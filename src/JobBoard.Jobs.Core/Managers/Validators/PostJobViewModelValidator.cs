using FluentValidation;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;

namespace JobBoard.Jobs.Core.Managers.Validators;

/// <summary>
/// Shape/format rules for <see cref="PostJobViewModel"/> — lengths mirror the EF configuration, so a
/// value that validates here fits the column. Data-dependent rules (does a category exist? is the job
/// open?) are <b>not</b> here; those need the database and live in the business layer.
/// </summary>
public sealed class PostJobViewModelValidator : AbstractValidator<PostJobViewModel>
{
    // URL-safe slug: lowercase letters, digits, and single hyphens (e.g. "senior-engineer").
    private const string SlugPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";

    public PostJobViewModelValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmployerId).NotEmpty();

        RuleFor(x => x.Salary).NotNull();
        When(x => x.Salary is not null, () =>
        {
            RuleFor(x => x.Salary.Min).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Salary.Max).GreaterThanOrEqualTo(x => x.Salary.Min)
                .WithMessage("'Salary Max' must be greater than or equal to 'Salary Min'.");
            RuleFor(x => x.Salary.Currency).NotEmpty().Length(3);
        });

        RuleForEach(x => x.Categories).SetValidator(new JobClassificationViewModelValidator(100));
        RuleForEach(x => x.Tags).SetValidator(new JobClassificationViewModelValidator(50));
    }

    /// <summary>
    /// Validates a single category/tag entry. <paramref name="maxNameLength"/> differs by kind
    /// (categories allow 100, tags 50) to match the respective EF configurations; slugs share the
    /// URL-safe format and are bounded by the same length.
    /// </summary>
    private sealed class JobClassificationViewModelValidator : AbstractValidator<JobClassificationViewModel>
    {
        public JobClassificationViewModelValidator(int maxNameLength)
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(maxNameLength);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(maxNameLength)
                .Matches(SlugPattern)
                .WithMessage("'Slug' must be lowercase letters, digits, and single hyphens.");
        }
    }
}
