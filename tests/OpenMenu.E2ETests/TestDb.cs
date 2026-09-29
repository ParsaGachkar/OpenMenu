using Microsoft.EntityFrameworkCore;
using OpenMenu.Domain;
using OpenMenu.Infrastructure.Auth;
using OpenMenu.Infrastructure.Data;

namespace OpenMenu.E2ETests;

/// <summary>
/// Ensures the accounts the E2E suite logs in with exist, directly via EF Core
/// against the same database the app uses. Idempotent and shared across tests.
/// The Admin account comes from the app's own seeding; the Editor account is
/// created here because the app intentionally has no registration.
/// </summary>
public static class TestDb
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static bool _ensured;

    public const string EditorUsername = "e2e-editor";
    public const string EditorPassword = "editor123";

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("OPENMENU_DB")
        ?? "Host=localhost;Port=5433;Database=openmenu;Username=postgres;Password=postgres";

    public static async Task EnsureUsersAsync()
    {
        if (_ensured)
        {
            return;
        }

        await Lock.WaitAsync();
        try
        {
            if (_ensured)
            {
                return;
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            await using var db = new AppDbContext(options);
            if (!await db.Users.AnyAsync(u => u.Username == EditorUsername))
            {
                db.Users.Add(new User
                {
                    Username = EditorUsername,
                    PasswordHash = PasswordHasher.Hash(EditorPassword),
                    Role = UserRole.Editor,
                    IsActive = true,
                });
                await db.SaveChangesAsync();
            }

            _ensured = true;
        }
        finally
        {
            Lock.Release();
        }
    }
}
