namespace JobBoard.Jobs.Core.Managers.Models.ViewModels;

/// <summary>
/// A category or tag supplied on a job post. Carries the display <see cref="Name"/> and the URL-safe
/// <see cref="Slug"/> that keys it — the post resolves an existing classification by slug or creates it.
/// </summary>
public sealed record JobClassificationViewModel
{
    public string Name { get; init; } = default!;

    public string Slug { get; init; } = default!;
}
