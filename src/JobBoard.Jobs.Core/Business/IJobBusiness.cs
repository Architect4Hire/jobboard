using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;

namespace JobBoard.Jobs.Core.Business;

/// <summary>
/// Domain rules, translation, and the decision to emit an event. Reads map entity → service model;
/// the post translates the view model → domain; the close applies the lifecycle rule, builds the
/// <c>JobClosed</c> event, and hands it to the data layer. Depends only on <see cref="Data.IJobDataLayer"/>.
/// </summary>
public interface IJobBusiness
{
    Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default);

    Task<JobDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<JobDetailServiceModel> PostAsync(PostJobViewModel viewModel, CancellationToken cancellationToken = default);

    Task<JobDetailServiceModel> CloseAsync(Guid id, CancellationToken cancellationToken = default);
}
