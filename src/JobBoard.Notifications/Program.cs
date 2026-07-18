using JobBoard.Contracts;
using JobBoard.Notifications.Consumers;
using JobBoard.Notifications.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// NotificationsDbContext via the Aspire Npgsql integration, keyed to the "notificationsdb" resource
// (connection injected by the AppHost — never a raw connection string).
builder.AddNpgsqlDbContext<NotificationsDbContext>("notificationsdb");

// ServiceBusClient via the Aspire integration, keyed to the "servicebus" resource, so the shared
// processor host (receive side) has a client to resolve.
builder.AddAzureServiceBusClient("servicebus");

// Composition: the .Core stack + the shared persistence/messaging spine, then the three consumers this
// service reacts to. The subscription strings MUST match the AppHost's subscriptions exactly.
builder.Services.AddNotificationsCore();
builder.Services.AddSharedPersistence<NotificationsDbContext>();
builder.Services.AddSharedMessaging<NotificationsDbContext>();
builder.Services.AddIntegrationEventConsumer<ApplicationSubmitted, ApplicationSubmittedConsumer>("notifications-submitted");
builder.Services.AddIntegrationEventConsumer<ApplicationStatusChanged, ApplicationStatusChangedConsumer>("notifications-status-changed");
builder.Services.AddIntegrationEventConsumer<JobPosted, JobPostedConsumer>("notifications-jobposted");

var app = builder.Build();

// Development convenience: apply the Notifications migrations to the freshly-provisioned notificationsdb
// container so `aspire run` is a single command. Guarded to Development — tests manage their own schema.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    await db.Database.MigrateAsync();
}

// No public HTTP surface — Notifications is event-only, so there are no controllers and no gateway route.
// Only the health/alive endpoints are mapped, so the dashboard can track it. Deliberately no
// AddSharedExceptionHandler/UseExceptionHandler either: that shapes HTTP error responses, and there is no
// request pipeline here — a consumer failure surfaces to the processor host, which leaves the message
// unsettled for redelivery. (AddSharedMessaging also wires the OutboxDispatcher; it idles against an empty
// outbox since Notifications publishes nothing — the receive-side processor host is what this service needs.)
app.MapDefaultEndpoints();

app.Run();

// Exposed so any host-level tests can reference the assembly's entry point if needed.
public partial class Program;
