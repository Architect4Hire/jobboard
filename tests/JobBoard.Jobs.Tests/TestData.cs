using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;

namespace JobBoard.Jobs.Tests;

/// <summary>Builders for the fixtures the Jobs tests share, kept terse and override-friendly.</summary>
internal static class TestData
{
    public static Job Job(
        Guid? id = null,
        JobStatus status = JobStatus.Open,
        string title = "Senior Engineer",
        IEnumerable<Category>? categories = null,
        IEnumerable<Tag>? tags = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = title,
        Description = "Build things.",
        Location = "Remote",
        Salary = new SalaryBand { Min = 100_000m, Max = 150_000m, Currency = "USD" },
        Status = status,
        EmployerId = Guid.NewGuid(),
        CreatedOnUtc = DateTime.UtcNow,
        Categories = categories?.ToList() ?? [],
        Tags = tags?.ToList() ?? [],
    };

    public static Category Category(string slug = "engineering", string name = "Engineering") =>
        new() { Id = Guid.NewGuid(), Name = name, Slug = slug };

    public static Tag Tag(string slug = "remote", string name = "Remote") =>
        new() { Id = Guid.NewGuid(), Name = name, Slug = slug };

    public static PostJobViewModel PostViewModel(
        string title = "Senior Engineer",
        Guid? employerId = null,
        IReadOnlyList<JobClassificationViewModel>? categories = null,
        IReadOnlyList<JobClassificationViewModel>? tags = null) => new()
    {
        Title = title,
        Description = "Build things.",
        Location = "Remote",
        Salary = new SalaryBandViewModel { Min = 100_000m, Max = 150_000m, Currency = "USD" },
        EmployerId = employerId ?? Guid.NewGuid(),
        Categories = categories ?? [new JobClassificationViewModel { Name = "Engineering", Slug = "engineering" }],
        Tags = tags ?? [new JobClassificationViewModel { Name = "Remote", Slug = "remote" }],
    };
}
