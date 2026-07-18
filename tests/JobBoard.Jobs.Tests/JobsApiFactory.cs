using JobBoard.Jobs.Core.Data;
using JobBoard.Shared.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace JobBoard.Jobs.Tests;

/// <summary>
/// Hosts the real Jobs pipeline (controllers → facade → business → data → repository + the shared
/// outbox) for endpoint tests, with two swaps that keep it self-contained: the Aspire Npgsql context
/// becomes SQLite over one always-open in-memory connection, and the Service Bus background services are
/// dropped (nothing to connect to). Runs outside Development, so the host's migrate-on-startup never fires.
/// </summary>
public sealed class JobsApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public JobsApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Dummy connection strings so the Aspire registrations bind without a real Postgres / Service Bus.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:jobsdb"] = "Host=localhost;Database=jobsdb;Username=test;Password=test",
            // Lets AddRedisDistributedCache bind; the registration is swapped for an in-memory cache below,
            // so nothing actually dials this address.
            ["ConnectionStrings:cache"] = "localhost:6379",
            ["ConnectionStrings:servicebus"] =
                "Endpoint=sb://localhost.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAA==",
        }));

        builder.ConfigureTestServices(services =>
        {
            // No Service Bus in tests — drop the relay/processor so nothing tries to open a connection.
            RemoveHostedService<OutboxDispatcher>(services);
            RemoveHostedService<ServiceBusProcessorHost>(services);

            // No Redis container in tests — replace the Aspire-registered Redis IDistributedCache with an
            // in-memory one. The facade's ICache → RedisCache path still runs (real JSON round-trip); it
            // just serializes into memory instead of dialing Redis.
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            // Swap the Npgsql context for SQLite over the shared, always-open connection. The Aspire
            // integration registers its provider through IDbContextOptionsConfiguration<JobsDbContext>,
            // so that has to go too or EF sees two providers configured.
            services.RemoveAll<IDbContextOptionsConfiguration<JobsDbContext>>();
            services.RemoveAll<DbContextOptions<JobsDbContext>>();
            services.RemoveAll<JobsDbContext>();
            services.AddDbContext<JobsDbContext>(options => options.UseSqlite(_connection));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<JobsDbContext>().Database.EnsureCreated();
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
