namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Jobs.Core stack (facade → business → data → repository + validators/mappers).
/// Empty for now — this step proves the wiring; layers are added with the first endpoint.
/// </summary>
public static class JobsCoreServiceCollectionExtensions
{
    public static IServiceCollection AddJobsCore(this IServiceCollection services)
    {
        // Layers registered here as they're introduced.
        return services;
    }
}
