using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Storage;
using JobBoard.Profiles.Tests.Fakes;
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
/// tests, with the Aspire Npgsql context swapped for SQLite over one always-open in-memory connection and
/// the blob-backed résumé storage swapped for an in-memory fake (so uploads never need Azurite). Runs
/// outside Development, so the host's migrate-on-startup never fires.
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
        }));

        builder.ConfigureTestServices(services =>
        {
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
