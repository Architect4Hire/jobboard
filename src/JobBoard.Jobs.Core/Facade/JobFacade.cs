using FluentValidation;
using JobBoard.Jobs.Core.Business;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;

namespace JobBoard.Jobs.Core.Facade;

/// <inheritdoc cref="IJobFacade"/>
/// <remarks>
/// Caching is intentionally deferred: no <c>cache</c> resource exists yet, so this facade owns the
/// validation seam now and read-through/invalidate of the service models lands when Redis is wired
/// (the facade is where it will go — never business or data).
/// </remarks>
public sealed class JobFacade : IJobFacade
{
    private readonly IJobBusiness _business;
    private readonly IValidator<PostJobViewModel> _postValidator;

    public JobFacade(IJobBusiness business, IValidator<PostJobViewModel> postValidator)
    {
        _business = business;
        _postValidator = postValidator;
    }

    public Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default) =>
        _business.ListAsync(categorySlug, cancellationToken);

    public Task<JobDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _business.GetAsync(id, cancellationToken);

    public async Task<JobDetailServiceModel> PostAsync(PostJobViewModel viewModel, CancellationToken cancellationToken = default)
    {
        // The global exception handler maps the thrown ValidationException to a 400 with field detail.
        await _postValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
        return await _business.PostAsync(viewModel, cancellationToken);
    }

    public Task<JobDetailServiceModel> CloseAsync(Guid id, CancellationToken cancellationToken = default) =>
        // No inbound view model to validate; the "must be open" check is a domain rule in business.
        _business.CloseAsync(id, cancellationToken);
}
