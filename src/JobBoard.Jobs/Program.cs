using JobBoard.Jobs.Core.Data;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// JobsDbContext via the Aspire Npgsql integration, keyed to the "jobsdb" resource
// (connection injected by the AppHost — never a raw connection string).
builder.AddNpgsqlDbContext<JobsDbContext>("jobsdb");

// ServiceBusClient via the Aspire integration, keyed to the "servicebus" resource,
// so AddSharedMessaging's dispatcher/processor host have a client to resolve.
builder.AddAzureServiceBusClient("servicebus");

// Composition: the .Core stack (empty for now) + the shared persistence/messaging spine.
builder.Services.AddJobsCore();
builder.Services.AddSharedPersistence<JobsDbContext>();
builder.Services.AddSharedMessaging<JobsDbContext>();
builder.Services.AddSharedExceptionHandler();

builder.Services.AddControllers();

var app = builder.Build();

app.UseExceptionHandler();

app.MapDefaultEndpoints();   // health/alive — drives the dashboard health state
app.MapControllers();        // no controllers yet; endpoints arrive via add-endpoint

app.Run();
