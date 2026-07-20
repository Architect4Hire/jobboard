namespace JobBoard.Jobs.Core.Managers.Models.Domain;

/// <summary>
/// A job posting — the aggregate root of the Jobs context. Categories and Tags are many-to-many
/// classifications; <see cref="EmployerId"/> is reference data duplicated from Identity (never a
/// cross-service FK — the join would cross a database boundary this architecture forbids).
/// </summary>
public class Job
{
    public Guid Id { get; set; }

    public string Title { get; set; } = default!;

    public string Description { get; set; } = default!;

    public string Location { get; set; } = default!;

    public SalaryBand Salary { get; set; } = default!;

    public JobStatus Status { get; set; }

    /// <summary>The employer that owns this posting; sourced from Identity, kept locally as a plain Guid.</summary>
    public Guid EmployerId { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
