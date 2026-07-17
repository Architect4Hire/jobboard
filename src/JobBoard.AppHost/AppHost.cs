var builder = DistributedApplication.CreateBuilder(args);

// Local PostgreSQL server (a container). One database per service is added off this server.
var postgres = builder.AddPostgres("postgres");
var jobsDb = postgres.AddDatabase("jobsdb");

// Azure Service Bus, run locally as the emulator container (plus its mssql companion) — no cloud
// resource. Topics/subscriptions are declared per event as services are introduced.
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

// The first integration event. The topic name matches the outbox row's Destination (the event-type
// name), so the dispatcher's sender finds it. The "applications" subscription is where the future
// Applications consumer will read; declaring it now means a relayed JobClosed message lands somewhere
// peekable on the emulator even though no consumer runs yet — which is exactly the demo.
serviceBus.AddServiceBusTopic("JobClosed")
    .AddServiceBusSubscription("applications");

// First bounded service: owns jobsdb and talks to the bus (outbox dispatcher + processor host).
var jobs = builder.AddProject<Projects.JobBoard_Jobs>("jobs")
    .WithReference(jobsDb)
    .WithReference(serviceBus)
    .WaitFor(jobsDb)
    .WaitFor(serviceBus);   // jobs runs the outbox dispatcher + processor host, so it uses the bus

// The gateway is the only public entry point. It references jobs so YARP can resolve the "http://jobs"
// destination through service discovery, and the bus (a placeholder until services consume it). The
// connection/discovery config is injected, never hardcoded.
var gateway = builder.AddProject<Projects.JobBoard_Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithReference(jobs)
    .WithReference(serviceBus)
    .WaitFor(jobs);

// The Angular app talks only to the gateway; Aspire injects the gateway base URL and the
// port to serve on — nothing is hardcoded.
builder.AddJavaScriptApp("web", "../web", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
