namespace JobBoard.Profiles.Core.Managers.Models.ViewModels;

/// <summary>
/// Inbound request to create-or-replace an employer company profile — the only shape the employer upsert
/// controller binds. The owning employer's id comes from the route, not this body.
/// </summary>
public sealed record UpsertEmployerProfileViewModel
{
    public string CompanyName { get; init; } = default!;

    public string? Website { get; init; }

    public string Description { get; init; } = default!;
}
