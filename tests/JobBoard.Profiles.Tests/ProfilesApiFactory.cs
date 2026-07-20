using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Storage;
using JobBoard.Profiles.Tests.Fakes;
using JobBoard.Shared.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace JobBoard.Profiles.Tests;

/// <summary>
/// Hosts the real Profiles pipeline (controllers → facade → business → data → repository + the shared
/// outbox) for endpoint tests, with three swaps that keep it self-contained: the Aspire Npgsql context
/// becomes SQLite over one always-open in-memory connection, the blob-backed résumé storage becomes an
/// in-memory fake (so uploads never need Azurite), and the Service Bus background services are dropped
/// (nothing to connect to). Runs outside Development, so the host's migrate-on-startup never fires.
/// </summary>
public sealed class ProfilesApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    /// <summary>The in-memory résumé store the hosted pipeline writes to — asserted on by upload tests.</summary>
    public InMemoryResumeStorage ResumeStorage { get; } = new();

    public ProfilesApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Dummy connection strings so the Aspire Npgsql + Blob registrations bind. The DbContext is swapped
        // for SQLite below and IResumeStorage for the in-memory fake, so neither real client is ever built.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:profilesdb"] = "Host=localhost;Database=profilesdb;Username=test;Password=test",
            ["ConnectionStrings:blobs"] = "UseDevelopmentStorage=true",
            // Dummy Service Bus connection so AddAzureServiceBusClient binds; the relay is dropped below.
            ["ConnectionStrings:servicebus"] =
                "Endpoint=sb://localhost.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAA==",
        }));

        builder.ConfigureTestServices(services =>
        {
            // No Service Bus in tests — drop the relay/processor so nothing tries to open a connection. The
            // ProfileUpdated outbox row is still written by the write paths; the data-layer tests assert it.
            RemoveHostedService<OutboxDispatcher>(services);
            RemoveHostedService<ServiceBusProcessorHost>(services);

            services.RemoveAll<IDbContextOptionsConfiguration<ProfilesDbContext>>();
            services.RemoveAll<DbContextOptions<ProfilesDbContext>>();
            services.RemoveAll<ProfilesDbContext>();
            services.AddDbContext<ProfilesDbContext>(options => options.UseSqlite(_connection));

            // Swap blob-backed résumé storage for the in-memory fake (singleton so uploads persist across
            // requests within a test) — the real BlobServiceClient is never resolved.
            services.RemoveAll<IResumeStorage>();
            services.AddSingleton<IResumeStorage>(ResumeStorage);

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ProfilesDbContext>().Database.EnsureCreated();
        });
    }

    private static void RemoveHostedService<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(T));

        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
