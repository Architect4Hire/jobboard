using JobBoard.Identity.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// IdentityDbContext via the Aspire Npgsql integration, keyed to the "identitydb" resource
// (connection injected by the AppHost — never a raw connection string).
builder.AddNpgsqlDbContext<IdentityDbContext>("identitydb");

// ServiceBusClient via the Aspire integration, keyed to the "servicebus" resource, so the outbox
// dispatcher (send side) has a client to resolve.
builder.AddAzureServiceBusClient("servicebus");

// Composition: the .Core stack (facade → business → data → repository + password/token seams +
// validators) + the shared persistence/messaging spine. Identity now publishes audit facts (account
// created, login) through its outbox (SCRUB A7): AddSharedPersistence wires IOutbox onto the request
// scope and AddSharedMessaging runs the OutboxDispatcher that relays them. It consumes no events, so the
// processor host AddSharedMessaging also starts opens no subscriptions (it idles).
builder.Services.AddIdentityCore(builder.Configuration);
builder.Services.AddSharedPersistence<IdentityDbContext>();
builder.Services.AddSharedMessaging<IdentityDbContext>();
builder.Services.AddSharedExceptionHandler();

// Ambient request context: reads the correlation thread the gateway projected (ADR-0015) so the audit
// events this service will emit (SCRUB A7) can carry it. Populated per request by UseSharedRequestContext.
builder.Services.AddSharedRequestContext();

builder.Services.AddControllers();

var app = builder.Build();

// Development convenience: apply the Identity migrations to the freshly-provisioned identitydb container
// so `aspire run` is a single command. Guarded to Development — tests manage their own schema.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();

    // Seed known demo accounts (idempotent) so a reviewer can sign in without registering.
    var hasher = scope.ServiceProvider.GetRequiredService<JobBoard.Identity.Core.Security.IPasswordHasher>();
    await JobBoard.Identity.Core.Seeding.IdentitySeedData.SeedAsync(db, hasher);
}

app.UseExceptionHandler();

// Read the trusted edge headers into the scoped IRequestContext before any endpoint runs.
app.UseSharedRequestContext();

app.MapDefaultEndpoints();   // health/alive — drives the dashboard health state
app.MapControllers();        // IdentityController — register/login

app.Run();

// Exposed so the endpoint integration tests can host the real pipeline via WebApplicationFactory<Program>.
public partial class Program;
