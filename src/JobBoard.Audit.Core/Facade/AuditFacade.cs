using FluentValidation;
using JobBoard.Audit.Core.Business;
using JobBoard.Audit.Core.Managers.Models.ServiceModels;
using JobBoard.Audit.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Audit.Core.Facade;

/// <inheritdoc cref="IAuditFacade"/>
public sealed class AuditFacade : IAuditFacade
{
    private readonly IAuditBusiness _business;
    private readonly IValidator<AuditQueryViewModel> _queryValidator;

    public AuditFacade(IAuditBusiness business, IValidator<AuditQueryViewModel> queryValidator)
    {
        _business = business;
        _queryValidator = queryValidator;
    }

    public Task RecordAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default) =>
        _business.RecordAsync(@event, cancellationToken);

    public async Task<IReadOnlyList<AuditEntryServiceModel>> QueryAsync(
        AuditQueryViewModel query,
        CancellationToken cancellationToken = default)
    {
        // The global exception handler maps the thrown ValidationException to a 400 with field detail.
        await _queryValidator.ValidateAndThrowAsync(query, cancellationToken);
        return await _business.QueryAsync(query, cancellationToken);
    }
}
