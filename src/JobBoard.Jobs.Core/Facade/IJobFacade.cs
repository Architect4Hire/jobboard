using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;

namespace JobBoard.Jobs.Core.Facade;

/// <summary>
/// The boundary the controller calls: validates inbound view models and read-through/invalidates the
/// cached job-list service models, then delegates to <see cref="Business.IJobBusiness"/>. No mapping, EF,
/// or bus here.
/// </summary>
public interface IJobFacade
{
    Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default);

    Task<JobDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<JobDetailServiceModel> PostAsync(PostJobViewModel viewModel, CancellationToken cancellationToken = default);

    Task<JobDetailServiceModel> CloseAsync(Guid id, CancellationToken cancellationToken = default);
}
