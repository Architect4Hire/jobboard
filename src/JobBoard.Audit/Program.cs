using JobBoard.Audit.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// AuditDbContext via the Aspire Npgsql integration, keyed to the "auditdb" resource (connection
// injected by the AppHost — never a raw connection string).
builder.AddNpgsqlDbContext<AuditDbContext>("auditdb");

// ServiceBusClient via the Aspire integration, keyed to the "servicebus" resource, so the shared
// processor host (receive side) has a client to resolve.
builder.AddAzureServiceBusClient("servicebus");

// Composition: the .Core stack + the shared persistence/messaging spine. The receive-side processor
// host comes from AddSharedMessaging; the audit consumers that subscribe to every business event are
// added in SCRUB A5 (AddIntegrationEventConsumer, one per audit-* subscription declared in the AppHost).
builder.Services.AddAuditCore();
builder.Services.AddSharedPersistence<AuditDbContext>();
builder.Services.AddSharedMessaging<AuditDbContext>();

var app = builder.Build();

// Development convenience: apply the Audit migrations to the freshly-provisioned auditdb container so
// `aspire run` is a single command. Guarded to Development — tests manage their own schema.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await db.Database.MigrateAsync();
}

// No public HTTP surface — Audit is consumer-only (the read-only support-query route is SCRUB A6), so
// there are no controllers and no gateway route yet. Only the health/alive endpoints are mapped, so the
// dashboard can track it. Deliberately no AddSharedExceptionHandler/UseExceptionHandler: that shapes HTTP
// error responses, and there is no request pipeline here — a consumer failure surfaces to the processor
// host, which leaves the message unsettled for redelivery. (AddSharedMessaging also wires the
// OutboxDispatcher; it idles against an empty outbox since Audit publishes nothing — the receive-side
// processor host is what this service needs.)
app.MapDefaultEndpoints();

app.Run();

// Exposed so any host-level tests can reference the assembly's entry point if needed.
public partial class Program;
