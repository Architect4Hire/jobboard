namespace JobBoard.Jobs.Core.Managers.Models.Domain;

/// <summary>
/// A broad classification a <see cref="Job"/> can belong to (e.g. "Engineering"). Many jobs to many
/// categories; <see cref="Slug"/> is the URL-safe unique key used for filtering.
/// </summary>
public class Category : IClassification
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
