using JobBoard.Profiles.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// ProfilesDbContext via the Aspire Npgsql integration, keyed to the "profilesdb" resource
// (connection injected by the AppHost — never a raw connection string).
builder.AddNpgsqlDbContext<ProfilesDbContext>("profilesdb");

// Composition: the .Core stacks (candidate + employer, each facade → business → data → repository +
// validators). Profiles publishes and consumes no integration events, so there is no Service Bus client
// and no shared messaging spine; it validates no tokens (the gateway does) — only the shared exception
// handler for the standard error shape.
builder.Services.AddProfilesCore();
builder.Services.AddSharedExceptionHandler();

builder.Services.AddControllers();

var app = builder.Build();

// Development convenience: apply the Profiles migrations to the freshly-provisioned profilesdb container
// so `aspire run` is a single command. Guarded to Development — tests manage their own schema.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();   // health/alive — drives the dashboard health state
app.MapControllers();        // CandidateProfilesController + EmployerProfilesController — get/upsert

app.Run();

// Exposed so the endpoint integration tests can host the real pipeline via WebApplicationFactory<Program>.
public partial class Program;
