using JobBoard.Jobs.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// JobsDbContext via the Aspire Npgsql integration, keyed to the "jobsdb" resource
// (connection injected by the AppHost — never a raw connection string).
builder.AddNpgsqlDbContext<JobsDbContext>("jobsdb");

// ServiceBusClient via the Aspire integration, keyed to the "servicebus" resource,
// so AddSharedMessaging's dispatcher/processor host have a client to resolve.
builder.AddAzureServiceBusClient("servicebus");

// Composition: the .Core stack (facade → business → data → repository + validators) + the shared
// persistence/messaging spine.
builder.Services.AddJobsCore();
builder.Services.AddSharedPersistence<JobsDbContext>();
builder.Services.AddSharedMessaging<JobsDbContext>();
builder.Services.AddSharedExceptionHandler();

builder.Services.AddControllers();

var app = builder.Build();

// Development convenience: apply the Jobs migrations to the freshly-provisioned jobsdb container so
// `aspire run` is a single command. Guarded to Development — tests and other environments manage their
// own schema (the endpoint tests run on SQLite outside Development, so this never fires there).
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();   // health/alive — drives the dashboard health state
app.MapControllers();        // JobsController — list/get/post/close

app.Run();

// Exposed so the endpoint integration tests can host the real pipeline via WebApplicationFactory<Program>.
public partial class Program;
