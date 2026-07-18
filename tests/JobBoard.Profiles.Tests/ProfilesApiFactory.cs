using JobBoard.Profiles.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobBoard.Profiles.Tests;

/// <summary>
/// Hosts the real Profiles pipeline (controllers → facade → business → data → repository) for endpoint
/// tests, with the Aspire Npgsql context swapped for SQLite over one always-open in-memory connection.
/// Profiles wires no Service Bus and validates no tokens, so there is nothing else to strip. Runs outside
/// Development, so the host's migrate-on-startup never fires.
/// </summary>
public sealed class ProfilesApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public ProfilesApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Dummy connection string so the Aspire Npgsql registration binds before we swap it for SQLite.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:profilesdb"] = "Host=localhost;Database=profilesdb;Username=test;Password=test",
        }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<ProfilesDbContext>>();
            services.RemoveAll<DbContextOptions<ProfilesDbContext>>();
            services.RemoveAll<ProfilesDbContext>();
            services.AddDbContext<ProfilesDbContext>(options => options.UseSqlite(_connection));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ProfilesDbContext>().Database.EnsureCreated();
        });
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
