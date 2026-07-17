var builder = DistributedApplication.CreateBuilder(args);

// Local PostgreSQL server (a container). One database per service is added off this server.
var postgres = builder.AddPostgres("postgres");
var jobsDb = postgres.AddDatabase("jobsdb");

// Azure Service Bus, run locally as the emulator container (plus its mssql companion) — no cloud
// resource. Topics/subscriptions are declared per event as services are introduced; none exist yet,
// so the emulator starts empty and the shared dispatcher/processor host stay dormant.
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

// First bounded service: owns jobsdb and talks to the bus (outbox dispatcher + processor host).
// No gateway route yet — this step proves the host/DbContext wiring only.
builder.AddProject<Projects.JobBoard_Jobs>("jobs")
    .WithReference(jobsDb)
    .WithReference(serviceBus)
    .WaitFor(jobsDb)
    .WaitFor(serviceBus);   // jobs runs the outbox dispatcher + processor host, so it uses the bus

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
