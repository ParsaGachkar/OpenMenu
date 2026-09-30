using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenMenu.Infrastructure.Data;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using OpenMenu.Domain;

namespace OpenMenu.IntegrationTests;

/// <summary>
/// Boots the real Web host fully in-memory: PostgreSQL is replaced by an
/// in-memory SQLite database (kept open for the host's lifetime via a held
/// connection) and migrations are skipped in favor of EnsureCreated — the
/// tests mock the external dependency, so no real database is required.
/// A test authentication scheme replaces cookie auth so the authorized admin
/// JSON APIs can be exercised without a browser.
/// </summary>
public sealed class OpenMenuFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // One open connection keeps the shared in-memory SQLite database alive.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        builder.UseSetting("ConnectionStrings:Default", "DataSource=:memory:");
        builder.UseSetting("Database:EnsureCreated", "true");

        builder.ConfigureServices(services =>
        {
            // Replace Npgsql with in-memory SQLite.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.AddAuthentication(options =>
                    {
                        options.DefaultScheme = TestAuthHandler.Scheme;
                        options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                        options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                    })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>Returns a client authenticated as the given role.</summary>
    public HttpClient CreateRoleClient(string role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        return client;
    }

    /// <summary>Returns a client authenticated as Admin.</summary>
    public HttpClient CreateAdminClient() => CreateRoleClient(UserRole.Admin.ToString());

    /// <summary>Returns a client with no authentication (401/302 challenge).</summary>
    public HttpClient CreateAnonymousClient() => CreateClient();
}

/// <summary>
/// Minimal always-authenticated handler; the request header decides the role
/// (docs pattern: integration tests with a fake authentication scheme). No
/// header means genuinely unauthenticated.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "TestScheme";
    public const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers[RoleHeader].ToString();
        if (string.IsNullOrEmpty(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "test-user"),
            new Claim(ClaimTypes.Role, role),
        ], authenticationType: Scheme);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
