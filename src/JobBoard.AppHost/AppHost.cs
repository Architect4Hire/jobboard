var builder = DistributedApplication.CreateBuilder(args);

// Local PostgreSQL server (a container). Per-service databases are added with each service —
// none exist yet in the skeleton.
var postgres = builder.AddPostgres("postgres");

// Azure Service Bus, run locally as the emulator container (plus its mssql companion) — no cloud
// resource. Topics/subscriptions are declared per event as services are introduced; none exist yet,
// so the emulator starts empty and the shared dispatcher/processor host stay dormant.
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

// The gateway is the only public entry point. It references the bus as the sole resource wired to it
// for now (a placeholder until the real service hosts consume it); the connection is injected, never
// hardcoded. No WaitFor — the gateway doesn't use the bus, so its startup shouldn't hinge on the emulator.
var gateway = builder.AddProject<Projects.JobBoard_Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithReference(serviceBus);

// The Angular app talks only to the gateway; Aspire injects the gateway base URL and the
// port to serve on — nothing is hardcoded.
builder.AddJavaScriptApp("web", "../web", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
