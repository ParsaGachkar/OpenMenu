using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OpenMenu.Domain;
using OpenMenu.Infrastructure.Auth;
using OpenMenu.Infrastructure.Data;

namespace OpenMenu.Web.Services;

/// <summary>Validates credentials and issues the cookie principal. Deliberately small.</summary>
public class AuthService(AppDbContext db)
{
    /// <summary>
    /// Verifies username/password. Returns null on failure. Does not reveal whether
    /// the username exists: unknown user, wrong password and inactive user all fail identically.
    /// </summary>
    public async Task<ClaimsPrincipal?> SignInAsync(string username, string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Application"));
    }
}
