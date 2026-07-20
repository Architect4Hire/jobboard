namespace JobBoard.Applications.Core.Managers.Models.Domain;

/// <summary>
/// A local, event-fed mirror of a job posting's title and owning employer — kept in sync by
/// <c>JobPostedConsumer</c> so <c>ListMineAsync</c> can render an application's job title without a
/// cross-service call (ADR-0012 option B). Reference data owned by Jobs, mirrored here as plain fields,
/// never a cross-service FK.
/// </summary>
public class JobReference
{
    public Guid JobId { get; set; }

    public string Title { get; set; } = default!;

    public Guid EmployerId { get; set; }
}
