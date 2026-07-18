using JobBoard.Identity.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobBoard.Identity.Tests;

/// <summary>
/// Hosts the real Identity pipeline (controllers → facade → business → data → repository + the
/// password/token seams) for endpoint tests, with the Aspire Npgsql context swapped for SQLite over one
/// always-open in-memory connection. Supplies a signing key via config (the AppHost injects it via env in
/// a real run). Runs outside Development, so the host's migrate-on-startup never fires.
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
            // The signing key the AppHost would inject via env; issuer/audience come from appsettings.json.
            ["Jwt:SigningKey"] = TestSigningKey,
        }));

        builder.ConfigureTestServices(services =>
        {
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
