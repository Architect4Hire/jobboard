using JobBoard.Contracts;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;
using JobBoard.Shared.Requests;

namespace JobBoard.Jobs.Core.Managers.Mappers;

/// <summary>
/// The three mapping seams the business layer owns: <b>ViewModel → Domain</b> (post),
/// <b>Domain → ServiceModel</b> (every response), and <b>Domain → integration event</b> (close). Kept
/// out of the layers themselves so the translation is one obvious place. The list-summary projection is
/// <i>not</i> here — the repository projects it in SQL.
/// </summary>
public static class JobMappers
{
    /// <summary>
    /// Translates the post request into a new open <see cref="Job"/>. Classifications become detached
    /// <see cref="Category"/>/<see cref="Tag"/> entities keyed by slug; the repository reconciles them
    /// against existing rows (the app-assigned ids here are used only for ones that don't yet exist).
    /// </summary>
    public static Job ToEntity(this PostJobViewModel vm) => new()
    {
        Id = Guid.NewGuid(),
        Title = vm.Title,
        Description = vm.Description,
        Location = vm.Location,
        Salary = new SalaryBand
        {
            Min = vm.Salary.Min,
            Max = vm.Salary.Max,
            Currency = vm.Salary.Currency,
        },
        Status = JobStatus.Open,
        EmployerId = vm.EmployerId,
        CreatedOnUtc = DateTime.UtcNow,
        Categories = vm.Categories
            .Select(c => new Category { Id = Guid.NewGuid(), Name = c.Name, Slug = c.Slug })
            .ToList(),
        Tags = vm.Tags
            .Select(t => new Tag { Id = Guid.NewGuid(), Name = t.Name, Slug = t.Slug })
            .ToList(),
    };

    /// <summary>Maps a loaded job (with its categories and tags) to the full response shape.</summary>
    public static JobDetailServiceModel ToDetailServiceModel(this Job job) => new(
        job.Id,
        job.Title,
        job.Description,
        job.Location,
        new SalaryBandServiceModel(job.Salary.Min, job.Salary.Max, job.Salary.Currency),
        job.Status,
        job.EmployerId,
        job.Categories.Select(c => new JobClassificationServiceModel(c.Name, c.Slug)).ToList(),
        job.Tags.Select(t => new JobClassificationServiceModel(t.Name, t.Slug)).ToList(),
        job.CreatedOnUtc);

    /// <summary>
    /// Builds the <see cref="JobPosted"/> fact for a job that has just been created, stamping a fresh
    /// event id (its outbox-row key and Service Bus <c>MessageId</c>) and the audit <paramref name="thread"/>
    /// (ADR-0013). Carries the denormalized title and location a consumer needs, and reuses the job's
    /// creation time as the posted time.
    /// </summary>
    public static JobPosted ToJobPosted(this Job job, AuditThread thread) =>
        new(Guid.NewGuid(), job.Id, job.EmployerId, job.Title, job.Location, job.CreatedOnUtc)
        {
            CorrelationId = thread.CorrelationId,
            CausationId = thread.CausationId,
            ActorId = thread.ActorId,
        };

    /// <summary>
    /// Builds the <see cref="JobClosed"/> fact for a job that has just been closed, stamping a fresh
    /// event id (its outbox-row key and Service Bus <c>MessageId</c>), the audit <paramref name="thread"/>
    /// (ADR-0013), and the close timestamp.
    /// </summary>
    public static JobClosed ToJobClosed(this Job job, AuditThread thread) =>
        new(Guid.NewGuid(), job.Id, job.EmployerId, DateTime.UtcNow)
        {
            CorrelationId = thread.CorrelationId,
            CausationId = thread.CausationId,
            ActorId = thread.ActorId,
        };
}
