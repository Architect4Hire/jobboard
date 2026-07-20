using JobBoard.Audit.Core.Data;
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

namespace JobBoard.Audit.Tests;

/// <summary>
/// Hosts the real Audit pipeline (AuditController → facade → business → data → repository) for the
/// support-query endpoint tests, with the same two swaps the Jobs factory uses: the Aspire Npgsql context
/// becomes SQLite over one always-open in-memory connection, and the Service Bus background services are
/// dropped (nothing to connect to). Runs outside Development, so the host's migrate-on-startup never fires.
/// </summary>
public sealed class AuditApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public AuditApiFactory()
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
            ["ConnectionStrings:auditdb"] = "Host=localhost;Database=auditdb;Username=test;Password=test",
            ["ConnectionStrings:servicebus"] =
                "Endpoint=sb://localhost.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAA==",
        }));

        builder.ConfigureTestServices(services =>
        {
            // No Service Bus in tests — drop the relay/processor so nothing tries to open a connection.
            RemoveHostedService<OutboxDispatcher>(services);
            RemoveHostedService<ServiceBusProcessorHost>(services);

            // Swap the Npgsql context for SQLite over the shared, always-open connection. The Aspire
            // integration registers its provider through IDbContextOptionsConfiguration<AuditDbContext>,
            // so that has to go too or EF sees two providers configured.
            services.RemoveAll<IDbContextOptionsConfiguration<AuditDbContext>>();
            services.RemoveAll<DbContextOptions<AuditDbContext>>();
            services.RemoveAll<AuditDbContext>();
            services.AddDbContext<AuditDbContext>(options => options.UseSqlite(_connection));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.EnsureCreated();
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
