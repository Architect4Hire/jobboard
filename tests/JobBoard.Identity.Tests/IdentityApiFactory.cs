using JobBoard.Identity.Core.Data;
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

namespace JobBoard.Identity.Tests;

/// <summary>
/// Hosts the real Identity pipeline (controllers → facade → business → data → repository + the
/// password/token seams + the shared outbox) for endpoint tests, with two swaps that keep it
/// self-contained: the Aspire Npgsql context becomes SQLite over one always-open in-memory connection, and
/// the Service Bus background services are dropped (nothing to connect to). Supplies a signing key via
/// config (the AppHost injects it via env in a real run). Runs outside Development, so the host's
/// migrate-on-startup never fires.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    // A base64 test signing key of ample length for HMAC-SHA256 — test-only, never a real secret.
    private const string TestSigningKey =
        "dGVzdC1zaWduaW5nLWtleS1mb3Itam9iYm9hcmQtaWRlbnRpdHktdW5pdC10ZXN0cy0wMTIzNDU2Nzg5";

    private readonly SqliteConnection _connection;

    public IdentityApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Dummy connection string so the Aspire Npgsql registration binds before we swap it for SQLite.
            ["ConnectionStrings:identitydb"] = "Host=localhost;Database=identitydb;Username=test;Password=test",
            // Dummy Service Bus connection so AddAzureServiceBusClient binds; the relay is dropped below, so
            // nothing actually dials it.
            ["ConnectionStrings:servicebus"] =
                "Endpoint=sb://localhost.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAA==",
            // The signing key the AppHost would inject via env; issuer/audience come from appsettings.json.
            ["Jwt:SigningKey"] = TestSigningKey,
        }));

        builder.ConfigureTestServices(services =>
        {
            // No Service Bus in tests — drop the relay/processor so nothing tries to open a connection. The
            // outbox row is still written by the register/login path; the endpoint tests assert the response,
            // and the data-layer tests assert the row.
            RemoveHostedService<OutboxDispatcher>(services);
            RemoveHostedService<ServiceBusProcessorHost>(services);

            // Swap the Npgsql context for SQLite over the shared, always-open connection. The Aspire
            // integration registers its provider through IDbContextOptionsConfiguration<IdentityDbContext>,
            // so that has to go too or EF sees two providers configured.
            services.RemoveAll<IDbContextOptionsConfiguration<IdentityDbContext>>();
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            services.RemoveAll<IdentityDbContext>();
            services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(_connection));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreated();
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
