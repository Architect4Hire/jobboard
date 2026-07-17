namespace JobBoard.Jobs.Core.Managers.Models.Domain;

/// <summary>
/// A fine-grained label a <see cref="Job"/> can carry (e.g. "remote", "dotnet"). Many jobs to many
/// tags; <see cref="Slug"/> is the URL-safe unique key used for filtering.
/// </summary>
public class Tag
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
