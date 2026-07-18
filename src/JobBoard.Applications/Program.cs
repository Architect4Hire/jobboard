using JobBoard.Applications.Consumers;
using JobBoard.Applications.Core.Data;
using JobBoard.Contracts;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// ApplicationsDbContext via the Aspire Npgsql integration, keyed to the "applicationsdb" resource
// (connection injected by the AppHost — never a raw connection string).
builder.AddNpgsqlDbContext<ApplicationsDbContext>("applicationsdb");

// ServiceBusClient via the Aspire integration, keyed to the "servicebus" resource, so AddSharedMessaging's
// dispatcher (send side) and processor host (receive side) have a client to resolve.
builder.AddAzureServiceBusClient("servicebus");

// Composition: the .Core stack (facade → business → data → repository + validators) + the shared
// persistence/messaging spine, plus the JobClosed consumer this service reacts to.
builder.Services.AddApplicationsCore();
builder.Services.AddSharedPersistence<ApplicationsDbContext>();
builder.Services.AddSharedMessaging<ApplicationsDbContext>();
builder.Services.AddSharedExceptionHandler();
builder.Services.AddIntegrationEventConsumer<JobClosed, JobClosedConsumer>("applications-jobclosed");

builder.Services.AddControllers();

var app = builder.Build();

// Development convenience: apply the Applications migrations to the freshly-provisioned applicationsdb
// container so `aspire run` is a single command. Guarded to Development — tests manage their own schema.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();   // health/alive — drives the dashboard health state
app.MapControllers();        // ApplicationsController — submit/withdraw/advance + reads

app.Run();

// Exposed so the endpoint integration tests can host the real pipeline via WebApplicationFactory<Program>.
public partial class Program;
