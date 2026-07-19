using JobBoard.Audit.Consumers;
using JobBoard.Audit.Core.Data;
using JobBoard.Contracts;
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
// host comes from AddSharedMessaging; then one generic AuditConsumer per business event records it to
// auditdb. The subscription strings MUST match the AppHost's audit-* subscriptions exactly (they share
// one Service Bus namespace).
builder.Services.AddAuditCore();
builder.Services.AddSharedPersistence<AuditDbContext>();
builder.Services.AddSharedMessaging<AuditDbContext>();
builder.Services.AddIntegrationEventConsumer<JobPosted, AuditConsumer<JobPosted>>("audit-jobposted");
builder.Services.AddIntegrationEventConsumer<JobClosed, AuditConsumer<JobClosed>>("audit-jobclosed");
builder.Services.AddIntegrationEventConsumer<ApplicationSubmitted, AuditConsumer<ApplicationSubmitted>>("audit-submitted");
builder.Services.AddIntegrationEventConsumer<ApplicationStatusChanged, AuditConsumer<ApplicationStatusChanged>>("audit-statuschanged");

// Read-only support-query surface (SCRUB A6): the AuditController and the shared exception handler that
// shapes a bad query filter into the shared error response. Reached only via the gateway's auth-protected
// /audit route — the service itself carries no auth (edge enforces it) and no mutation endpoints.
builder.Services.AddSharedExceptionHandler();
builder.Services.AddControllers();

var app = builder.Build();

// Development convenience: apply the Audit migrations to the freshly-provisioned auditdb container so
// `aspire run` is a single command. Guarded to Development — tests manage their own schema.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await db.Database.MigrateAsync();
}

// The only HTTP surface is the read-only support-query route (SCRUB A6) — Audit records via the bus and
// exposes just this one read path; there are no mutation endpoints and no auth here (the gateway's
// /audit route enforces auth at the edge). UseExceptionHandler shapes a bad query filter (a thrown
// ValidationException) into the shared error response. (AddSharedMessaging also wires the OutboxDispatcher;
// it idles against an empty outbox since Audit publishes nothing — the receive-side processor host and,
// now, this query surface are what this service needs. A consumer failure still surfaces to the processor
// host, which leaves the message unsettled for redelivery.)
app.UseExceptionHandler();

app.MapDefaultEndpoints();   // health/alive — drives the dashboard health state
app.MapControllers();        // AuditController — the support-query surface

app.Run();

// Exposed so any host-level tests can reference the assembly's entry point if needed.
public partial class Program;
