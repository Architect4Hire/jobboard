var builder = DistributedApplication.CreateBuilder(args);

// Local PostgreSQL server (a container). One database per service is added off this server.
var postgres = builder.AddPostgres("postgres");
var jobsDb = postgres.AddDatabase("jobsdb");
var applicationsDb = postgres.AddDatabase("applicationsdb");

// Azure Service Bus, run locally as the emulator container (plus its mssql companion) — no cloud
// resource. Topics/subscriptions are declared per event as services are introduced.
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

// The first integration event. The topic name matches the outbox row's Destination (the event-type
// name), so the dispatcher's sender finds it. The subscription is where the Applications consumer reads.
// Resource names are GLOBALLY unique across every type in the model, so the subscription can't just be
// "applications" (that's the service's project resource name) — it names the (service, event) pair.
serviceBus.AddServiceBusTopic("JobClosed")
    .AddServiceBusSubscription("applications-jobclosed");

// Events Applications publishes. Topic name = event-type name (the outbox Destination convention), so
// the dispatcher's sender finds them. Each forward-declares the future Notifications subscription so a
// relayed message lands somewhere peekable on the emulator — the same pattern used for JobClosed's
// "applications" subscription above. Subscription names are GLOBALLY unique in the Aspire model (not
// per-topic), so they carry the topic in the name.
serviceBus.AddServiceBusTopic("ApplicationSubmitted")
    .AddServiceBusSubscription("notifications-submitted");
serviceBus.AddServiceBusTopic("ApplicationStatusChanged")
    .AddServiceBusSubscription("notifications-status-changed");

// First bounded service: owns jobsdb and talks to the bus (outbox dispatcher + processor host).
var jobs = builder.AddProject<Projects.JobBoard_Jobs>("jobs")
    .WithReference(jobsDb)
    .WithReference(serviceBus)
    .WaitFor(jobsDb)
    .WaitFor(serviceBus);   // jobs runs the outbox dispatcher + processor host, so it uses the bus

// Second bounded service: owns applicationsdb, publishes ApplicationSubmitted/ApplicationStatusChanged,
// and consumes Jobs' JobClosed (via the "applications" subscription declared above).
var applications = builder.AddProject<Projects.JobBoard_Applications>("applications")
    .WithReference(applicationsDb)
    .WithReference(serviceBus)
    .WaitFor(applicationsDb)
    .WaitFor(serviceBus);   // runs the outbox dispatcher + processor host, so it uses the bus

// The gateway is the only public entry point. It references jobs so YARP can resolve the "http://jobs"
// destination through service discovery, and the bus (a placeholder until services consume it). The
// connection/discovery config is injected, never hardcoded.
var gateway = builder.AddProject<Projects.JobBoard_Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithReference(jobs)
    .WithReference(applications)
    .WithReference(serviceBus)
    .WaitFor(jobs)
    .WaitFor(applications);

// The Angular app talks only to the gateway; Aspire injects the gateway base URL and the
// port to serve on — nothing is hardcoded.
builder.AddJavaScriptApp("web", "../web", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
