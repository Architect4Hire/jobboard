namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Audit.Core stack. The host's composition root calls this alongside the shared
/// persistence/messaging spine.
/// </summary>
/// <remarks>
/// Intentionally empty for now (SCRUB A4 stands up the wiring only). The append data-layer/repository
/// and the audit consumers land in SCRUB A5, registered here and in the host respectively. There is no
/// validators or outbox usage — Audit is consumer-only and validates no ViewModels.
/// </remarks>
public static class AuditCoreServiceCollectionExtensions
{
    public static IServiceCollection AddAuditCore(this IServiceCollection services)
    {
        return services;
    }
}
