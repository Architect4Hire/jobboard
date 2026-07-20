using JobBoard.Profiles.Core.Data;
using JobBoard.Shared.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// ProfilesDbContext via the Aspire Npgsql integration, keyed to the "profilesdb" resource
// (connection injected by the AppHost — never a raw connection string).
builder.AddNpgsqlDbContext<ProfilesDbContext>("profilesdb");

// BlobServiceClient for résumé storage, keyed to the AppHost "blobs" resource (Azurite locally). The
// connection is injected by Aspire; IResumeStorage in .Core types against this client.
builder.AddAzureBlobServiceClient("blobs");

// ServiceBusClient via the Aspire integration, keyed to the "servicebus" resource, so the outbox
// dispatcher (send side) has a client to resolve.
builder.AddAzureServiceBusClient("servicebus");

// Composition: the .Core stacks (candidate + employer, each facade → business → data → repository +
// validators) + the shared persistence/messaging spine. Profiles now publishes ProfileUpdated audit facts
// through its outbox (SCRUB A7): AddSharedPersistence wires IOutbox onto the request scope and
// AddSharedMessaging runs the OutboxDispatcher that relays them. It consumes no events, so the processor
// host AddSharedMessaging also starts opens no subscriptions (it idles). It validates no tokens (the
// gateway does) — only the shared exception handler for the standard error shape.
builder.Services.AddProfilesCore();
builder.Services.AddSharedPersistence<ProfilesDbContext>();
builder.Services.AddSharedMessaging<ProfilesDbContext>();
builder.Services.AddSharedExceptionHandler();

// Ambient request context: reads the correlation/actor thread the gateway projected (ADR-0015) so the
// audit events this service will emit (SCRUB A7) can carry it. Populated per request by the middleware.
builder.Services.AddSharedRequestContext();

builder.Services.AddControllers();

var app = builder.Build();

// Development convenience: apply the Profiles migrations to the freshly-provisioned profilesdb container
// so `aspire run` is a single command. Guarded to Development — tests manage their own schema.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();
    await db.Database.MigrateAsync();

    // Seed demo candidate/employer profiles for the seeded accounts (idempotent). IOutbox resolves
    // against this same scope's ProfilesDbContext (AddSharedMessaging<TContext>), so a newly seeded
    // employer's EmployerProfileChanged row lands in the same SaveChangesAsync as the profile itself.
    var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
    await JobBoard.Profiles.Core.Seeding.ProfilesSeedData.SeedAsync(db, outbox);
}

app.UseExceptionHandler();

// Read the trusted edge headers into the scoped IRequestContext before any endpoint runs.
app.UseSharedRequestContext();

app.MapDefaultEndpoints();   // health/alive — drives the dashboard health state
app.MapControllers();        // CandidateProfilesController + EmployerProfilesController — get/upsert

app.Run();

// Exposed so the endpoint integration tests can host the real pipeline via WebApplicationFactory<Program>.
public partial class Program;
