var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// The gateway is the only public door. Routes/clusters are loaded from configuration; the
// service-discovery destination resolver lets YARP resolve cluster destinations named by Aspire
// resource ("http://jobs") through discovery instead of a literal DNS lookup.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();
