using System.Security.Cryptography;

var builder = DistributedApplication.CreateBuilder(args);

// Local PostgreSQL server (a container). One database per service is added off this server.
var postgres = builder.AddPostgres("postgres");
var jobsDb = postgres.AddDatabase("jobsdb");
var applicationsDb = postgres.AddDatabase("applicationsdb");
var identityDb = postgres.AddDatabase("identitydb");
var profilesDb = postgres.AddDatabase("profilesdb");

// Shared HMAC signing key for the JWTs Identity issues and the gateway validates. Kept out of source:
// supplied via AppHost config/user-secrets (Jwt:SigningKey) when present, otherwise a per-run generated
// dev key so `aspire run` stays a single command. Injected identically to both services via env — never
// a hardcoded literal. (Aspire exposes no auto-generated-secret parameter helper, so this is the wiring.)
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

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

// Identity: owns identitydb, issues JWTs. Synchronous request/response service — it publishes and
// consumes no integration events, so it takes no Service Bus reference. The signing key is injected via
// env (Jwt__SigningKey) so it stays out of source.
var identity = builder.AddProject<Projects.JobBoard_Identity>("identity")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);

// Profiles: owns profilesdb (candidate résumés + employer company profiles). Synchronous
// request/response service — it publishes/consumes no events (no Service Bus) and validates no tokens
// (the gateway does), so it takes neither a bus reference nor the signing key.
var profiles = builder.AddProject<Projects.JobBoard_Profiles>("profiles")
    .WithReference(profilesDb)
    .WaitFor(profilesDb);

// The gateway is the only public entry point. It references each proxied service so YARP can resolve the
// "http://<service>" destinations through service discovery, and the bus (a placeholder until services
// consume it). It validates the JWTs Identity signs, so it gets the same signing key via env. The
// connection/discovery config is injected, never hardcoded.
var gateway = builder.AddProject<Projects.JobBoard_Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithReference(jobs)
    .WithReference(applications)
    .WithReference(identity)
    .WithReference(profiles)
    .WithReference(serviceBus)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WaitFor(jobs)
    .WaitFor(applications)
    .WaitFor(identity)
    .WaitFor(profiles);

// The Angular app talks only to the gateway; Aspire injects the gateway base URL and the
// port to serve on — nothing is hardcoded.
builder.AddJavaScriptApp("web", "../web", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
