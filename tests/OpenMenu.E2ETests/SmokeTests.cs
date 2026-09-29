using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>Core flows: public menu, auth redirects, login, logout, role gates.</summary>
public class SmokeTests : E2ETestBase
{
    public SmokeTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PublicMenu_IsAccessibleWithoutLogin()
    {
        await Page.GotoAsync("/");

        // Restaurant name from the single-row settings.
        await Expect(Page.Locator("h1").First).Not.ToBeEmptyAsync();
        // Seeded demo data renders.
        await Expect(Page.GetByText("Hummus with Pita")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminPages_RedirectAnonymousUsersToLogin()
    {
        await Page.GotoAsync("/admin");
        await Page.WaitForURLAsync("**/login?ReturnUrl=%2Fadmin");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsGenericError()
    {
        await Page.GotoAsync("/login");
        await Page.GetByLabel("Username").FillAsync("admin");
        await Page.GetByLabel("Password").FillAsync("wrong-password");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        await Expect(Page.GetByText("Invalid username or password.")).ToBeVisibleAsync();
        // Still on the login page — no navigation happened.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
    }

    [Fact]
    public async Task Admin_CanLoginAndLogout()
    {
        await LoginAsAdminAsync();

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Logout" })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync(u => u == "http://localhost:8088/");

        // After logout the admin area is locked again.
        await Page.GotoAsync("/admin");
        await Page.WaitForURLAsync("**/login?ReturnUrl=%2Fadmin");
    }

    [Fact]
    public async Task Editor_CanOpenMenu_ButNotUsers()
    {
        await TestDb.EnsureUsersAsync();

        await Page.GotoAsync("/login");
        await Page.GetByLabel("Username").FillAsync(TestDb.EditorUsername);
        await Page.GetByLabel("Password").FillAsync(TestDb.EditorPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin");

        // Editor can manage menu content...
        await Page.GotoAsync("/admin/menu");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Menu items" })).ToBeVisibleAsync();

        // ...and gets bounced from user management.
        await Page.GotoAsync("/admin/users");
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/forbidden"));
        await Expect(Page.GetByText("403")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminNavbar_HidesUsersLink_ForEditors()
    {
        await TestDb.EnsureUsersAsync();

        await Page.GotoAsync("/login");
        await Page.GetByLabel("Username").FillAsync(TestDb.EditorUsername);
        await Page.GetByLabel("Password").FillAsync(TestDb.EditorPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin");

        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Users" })).ToHaveCountAsync(0);
    }
}
