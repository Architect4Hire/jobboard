namespace JobBoard.Applications.Core.Managers.Models.Domain;

/// <summary>
/// A local, event-fed mirror of an employer's company name — kept in sync by
/// <c>EmployerProfileChangedConsumer</c> so <c>ListMineAsync</c> can render the employer name without a
/// cross-service call (ADR-0012 option B). Reference data owned by Profiles, mirrored here as a plain
/// field, never a cross-service FK.
/// </summary>
public class EmployerReference
{
    public Guid EmployerId { get; set; }

    public string CompanyName { get; set; } = default!;
}
