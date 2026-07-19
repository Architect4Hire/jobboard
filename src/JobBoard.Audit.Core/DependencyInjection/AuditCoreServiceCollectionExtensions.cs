using JobBoard.Audit.Core.Business;
using JobBoard.Audit.Core.Data;
using JobBoard.Audit.Core.Facade;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Audit.Core stack (facade → business → data → repository). The host's composition root
/// calls this alongside the shared persistence/messaging spine. No validators (events aren't validated)
/// and no outbox usage (Audit is consumer-only and publishes nothing) — only the append path.
/// </summary>
public static class AuditCoreServiceCollectionExtensions
{
    public static IServiceCollection AddAuditCore(this IServiceCollection services)
    {
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IAuditDataLayer, AuditDataLayer>();
        services.AddScoped<IAuditBusiness, AuditBusiness>();
        services.AddScoped<IAuditFacade, AuditFacade>();

        return services;
    }
}
