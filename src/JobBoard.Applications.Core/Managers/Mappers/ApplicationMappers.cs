using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Applications.Core.Managers.Mappers;

/// <summary>
/// The three mapping seams the business layer uses: <see cref="ToEntity"/> (view model → new domain
/// entity), <see cref="ToDetailServiceModel"/> (domain → outbound service model), and the event builders
/// <see cref="ToApplicationSubmitted"/> / <see cref="ToStatusChanged"/> (domain → the <c>Contracts</c>
/// records, each stamping a fresh event <c>Id</c>). The summary projection is done in SQL by the
/// repository, so it isn't here.
/// </summary>
public static class ApplicationMappers
{
    public static Application ToEntity(this SubmitApplicationViewModel vm)
    {
        var now = DateTime.UtcNow;
        return new Application
        {
            Id = Guid.NewGuid(),
            CandidateId = vm.CandidateId,
            JobId = vm.JobId,
            ResumeReference = vm.ResumeReference,
            Status = ApplicationStatus.Submitted,
            SubmittedOnUtc = now,
            StatusChangedOnUtc = now,
        };
    }

    public static ApplicationDetailServiceModel ToDetailServiceModel(this Application application) => new(
        application.Id,
        application.CandidateId,
        application.JobId,
        application.Status,
        application.ResumeReference,
        application.SubmittedOnUtc,
        application.StatusChangedOnUtc);

    // Builds the ApplicationSubmitted fact — stamps a fresh event id.
    public static ApplicationSubmitted ToApplicationSubmitted(this Application application) =>
        new(Guid.NewGuid(), application.Id, application.CandidateId, application.JobId, application.SubmittedOnUtc);

    // Builds the ApplicationStatusChanged fact — stamps a fresh event id; statuses are carried as strings
    // (Contracts never references the ApplicationStatus enum).
    public static ApplicationStatusChanged ToStatusChanged(
        this Application application,
        ApplicationStatus from,
        ApplicationStatus to) =>
        new(
            Guid.NewGuid(),
            application.Id,
            application.CandidateId,
            application.JobId,
            from.ToString(),
            to.ToString(),
            DateTime.UtcNow);
}
