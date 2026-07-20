using FluentValidation;
using JobBoard.Audit.Core;
using JobBoard.Audit.Core.Business;
using JobBoard.Audit.Core.Data;
using JobBoard.Audit.Core.Facade;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Audit.Core stack (facade → business → data → repository) plus the support-query filter
/// validator. The host's composition root calls this alongside the shared persistence/messaging spine. No
/// outbox usage (Audit is consumer-only and publishes nothing) — the write path only appends, the read path
/// only queries.
/// </summary>
public static class AuditCoreServiceCollectionExtensions
{
    public static IServiceCollection AddAuditCore(this IServiceCollection services)
    {
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IAuditDataLayer, AuditDataLayer>();
        services.AddScoped<IAuditBusiness, AuditBusiness>();
        services.AddScoped<IAuditFacade, AuditFacade>();

        // Validators (the support-query filter — SCRUB A6) — registered once from this assembly.
        services.AddValidatorsFromAssemblyContaining<AuditCoreMarker>();

        return services;
    }
}
