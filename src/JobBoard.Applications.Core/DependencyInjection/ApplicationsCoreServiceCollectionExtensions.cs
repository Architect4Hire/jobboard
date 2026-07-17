using FluentValidation;
using JobBoard.Applications.Core;
using JobBoard.Applications.Core.Business;
using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Facade;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the whole Applications <c>.Core</c> stack (facade → business → data layer → repository) and
/// the view-model validators from this assembly. The host's composition root calls this alongside the
/// Shared persistence/messaging/exception extensions — no per-layer wiring lives in the host.
/// </summary>
public static class ApplicationsCoreServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationsCore(this IServiceCollection services)
    {
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IApplicationDataLayer, ApplicationDataLayer>();
        services.AddScoped<IApplicationBusiness, ApplicationBusiness>();
        services.AddScoped<IApplicationFacade, ApplicationFacade>();

        services.AddValidatorsFromAssemblyContaining<ApplicationsCoreMarker>();
        return services;
    }
}
