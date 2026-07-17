using FluentValidation;
using JobBoard.Jobs.Core;
using JobBoard.Jobs.Core.Business;
using JobBoard.Jobs.Core.Data;
using JobBoard.Jobs.Core.Facade;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Jobs.Core stack (facade → business → data → repository) and the validators from this
/// assembly. The host's composition root calls this alongside the shared persistence/messaging
/// extensions; no per-layer wiring lives in the host.
/// </summary>
public static class JobsCoreServiceCollectionExtensions
{
    public static IServiceCollection AddJobsCore(this IServiceCollection services)
    {
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobDataLayer, JobDataLayer>();
        services.AddScoped<IJobBusiness, JobBusiness>();
        services.AddScoped<IJobFacade, JobFacade>();

        // Validators (e.g. PostJobViewModelValidator) — registered once from this assembly.
        services.AddValidatorsFromAssemblyContaining<JobsCoreMarker>();

        return services;
    }
}
