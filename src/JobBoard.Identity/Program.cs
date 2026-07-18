using JobBoard.Identity.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// IdentityDbContext via the Aspire Npgsql integration, keyed to the "identitydb" resource
// (connection injected by the AppHost — never a raw connection string).
builder.AddNpgsqlDbContext<IdentityDbContext>("identitydb");

// Composition: the .Core stack (facade → business → data → repository + password/token seams +
// validators). Identity publishes and consumes no integration events, so there is no Service Bus client
// and no shared messaging spine — only the shared exception handler for the standard error shape.
builder.Services.AddIdentityCore(builder.Configuration);
builder.Services.AddSharedExceptionHandler();

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

app.MapDefaultEndpoints();   // health/alive — drives the dashboard health state
app.MapControllers();        // IdentityController — register/login

app.Run();

// Exposed so the endpoint integration tests can host the real pipeline via WebApplicationFactory<Program>.
public partial class Program;
