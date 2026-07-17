var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// The gateway is the only public door. Routes/clusters are loaded from configuration
// (empty for now — added per client-facing endpoint as services come online).
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();
