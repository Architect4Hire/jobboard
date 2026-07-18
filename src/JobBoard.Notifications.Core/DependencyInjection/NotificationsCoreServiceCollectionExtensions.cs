using JobBoard.Notifications.Core.Business;
using JobBoard.Notifications.Core.Data;
using JobBoard.Notifications.Core.Facade;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Notifications.Core stack (facade → business → data → repository). The host's composition
/// root calls this alongside the shared persistence/messaging spine. No validators (events aren't
/// validated) and no outbox usage (Notifications publishes nothing).
/// </summary>
public static class NotificationsCoreServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsCore(this IServiceCollection services)
    {
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationDataLayer, NotificationDataLayer>();
        services.AddScoped<INotificationBusiness, NotificationBusiness>();
        services.AddScoped<INotificationFacade, NotificationFacade>();

        return services;
    }
}
