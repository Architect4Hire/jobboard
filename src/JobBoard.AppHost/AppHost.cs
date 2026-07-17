var builder = DistributedApplication.CreateBuilder(args);

// Local PostgreSQL server (a container). Per-service databases are added with each service —
// none exist yet in the skeleton.
var postgres = builder.AddPostgres("postgres");

// The gateway is the only public entry point.
var gateway = builder.AddProject<Projects.JobBoard_Gateway>("gateway")
    .WithExternalHttpEndpoints();

// The Angular app talks only to the gateway; Aspire injects the gateway base URL and the
// port to serve on — nothing is hardcoded.
builder.AddJavaScriptApp("web", "../web", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
