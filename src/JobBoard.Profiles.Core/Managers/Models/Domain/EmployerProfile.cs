namespace JobBoard.Profiles.Core.Managers.Models.Domain;

/// <summary>
/// An employer's public company profile — the aggregate root of the employer side of the Profiles
/// context. <see cref="Id"/> <b>is</b> the employer's account id (sourced from Identity, kept locally as
/// a plain Guid); one profile per employer.
/// </summary>
public class EmployerProfile
{
    /// <summary>The owning employer's account id; also this profile's primary key (1:1 with the account).</summary>
    public Guid Id { get; set; }

    public string CompanyName { get; set; } = default!;

    public string? Website { get; set; }

    public string Description { get; set; } = default!;

    public DateTime UpdatedOnUtc { get; set; }
}
