using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using OpenMenu.Domain;
using OpenMenu.Infrastructure;
using OpenMenu.Infrastructure.Admin;
using OpenMenu.Infrastructure.Data;
using OpenMenu.Web.Components;
using OpenMenu.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents()
        .AddInteractiveWebAssemblyComponents()
        .AddAuthenticationStateSerialization();

// Localization (https://learn.microsoft.com/aspnet/core/blazor/globalization-localization).
builder.Services.AddLocalization();

// Cookie authentication (no ASP.NET Core Identity).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/forbidden";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Cookie.Name = "OpenMenu.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole(UserRole.Admin.ToString()))
    .AddPolicy("EditorOrAdmin", p => p.RequireRole(UserRole.Admin.ToString(), UserRole.Editor.ToString()));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthService>();

// Admin pages run with InteractiveAuto: they first render on a server circuit,
// then on WebAssembly. The docs pattern is one interface with an implementation
// per environment — AdminApiServer here (direct DbContext), AdminApiClient in
// the client project (HttpClient against the JSON endpoints below).
// https://learn.microsoft.com/aspnet/core/blazor/call-web-api
builder.Services.AddScoped<IAdminApi, AdminApiServer>();

// Persist Data Protection keys when the container mounts a key directory,
// so auth cookies survive restarts. No-op in local development.
const string keyRingPath = "/app/keys";
if (Directory.Exists(keyRingPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
}

builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=openmenu;Username=postgres;Password=postgres");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Serves wwwroot assets and the Blazor framework files (blazor.web.js,
// WASM runtime) via the build-time static-asset manifest (.NET 9+ standard).
app.MapStaticAssets();

// Request localization: honor the .AspNetCore.Culture cookie set by /culture/set,
// falling back to Accept-Language and then English. Placed per docs immediately
// after static files, before any component/endpoints rendering.
var supportedCultures = new[] { "en", "fa", "tr", "ar" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

// Docs-required order: authentication and authorization must run BEFORE the
// antiforgery middleware, so tokens minted for an authenticated user validate
// against the correct ClaimsPrincipal.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(OpenMenu.Web.Client._Imports).Assembly);

// POST logout from the interactive-auto (WebAssembly) admin layout: forms
// rendered by interactive components cannot embed an antiforgery token, so
// the endpoint opts out per the docs (a forced logout is a benign CSRF risk).
app.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.Redirect("/");
}).DisableAntiforgery();

// Culture selection (documented redirect-based approach): set the localization
// cookie and return to the referring page. GET is safe here — this endpoint
// only writes a preference cookie, never data.
app.MapGet("/culture/set", (string culture, string? redirectTo, HttpContext ctx) =>
{
    if (!supportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
    {
        return Results.BadRequest();
    }

    ctx.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)));

    // Only follow same-origin relative paths.
    var target = string.IsNullOrWhiteSpace(redirectTo) || !redirectTo.StartsWith('/') || redirectTo.StartsWith("//")
        ? "/"
        : redirectTo;
    return Results.LocalRedirect(target);
});

// ---- Admin JSON APIs: used by AdminApiClient (the WebAssembly render path of
//      the admin pages). Each endpoint is a thin wrapper over IAdminApi, so
//      both render paths share one implementation of the logic. Antiforgery
//      note (per docs): JSON requests are exempt from antiforgery validation
//      because browsers cannot send application/json cross-site without CORS;
//      the SameSite=Lax auth cookie provides the remaining guard. Deletes are
//      POST (not GET) so prefetchers can't trigger them. ----

app.MapGet("/api/admin/categories", async (IAdminApi api) =>
    Results.Ok(await api.GetCategoriesAsync())
).RequireAuthorization("EditorOrAdmin");

app.MapPost("/api/admin/categories", async (CategoryInput input, IAdminApi api) =>
    ToHttpResult(await api.SaveCategoryAsync(null, input), $"/api/admin/categories")
).RequireAuthorization("EditorOrAdmin");

app.MapPut("/api/admin/categories/{id:int}", async (int id, CategoryInput input, IAdminApi api) =>
    ToHttpResult(await api.SaveCategoryAsync(id, input))
).RequireAuthorization("EditorOrAdmin");

app.MapPost("/api/admin/categories/{id:int}/delete", async (int id, IAdminApi api) =>
    ToHttpResult(await api.DeleteCategoryAsync(id))
).RequireAuthorization("EditorOrAdmin");

app.MapGet("/api/admin/menu-items", async (IAdminApi api) =>
    Results.Ok(await api.GetMenuItemsAsync())
).RequireAuthorization("EditorOrAdmin");

app.MapPost("/api/admin/menu-items", async (MenuItemInput input, IAdminApi api) =>
    ToHttpResult(await api.SaveMenuItemAsync(null, input), "/api/admin/menu-items")
).RequireAuthorization("EditorOrAdmin");

app.MapPut("/api/admin/menu-items/{id:int}", async (int id, MenuItemInput input, IAdminApi api) =>
    ToHttpResult(await api.SaveMenuItemAsync(id, input))
).RequireAuthorization("EditorOrAdmin");

app.MapPost("/api/admin/menu-items/{id:int}/delete", async (int id, IAdminApi api) =>
    ToHttpResult(await api.DeleteMenuItemAsync(id))
).RequireAuthorization("EditorOrAdmin");

app.MapGet("/api/admin/users", async (IAdminApi api) =>
    Results.Ok(await api.GetUsersAsync())
).RequireAuthorization("AdminOnly");

app.MapPost("/api/admin/users", async (UserInput input, IAdminApi api) =>
    ToHttpResult(await api.CreateUserAsync(input), "/api/admin/users")
).RequireAuthorization("AdminOnly");

app.MapPut("/api/admin/users/{id:int}", async (int id, UserUpdateInput input, IAdminApi api) =>
    ToHttpResult(await api.UpdateUserAsync(id, input))
).RequireAuthorization("AdminOnly");

app.MapPost("/api/admin/users/{id:int}/password", async (int id, PasswordInput input, IAdminApi api) =>
    ToHttpResult(await api.SetUserPasswordAsync(id, input.Password))
).RequireAuthorization("AdminOnly");

app.MapGet("/api/admin/settings", async (IAdminApi api) =>
    Results.Ok(await api.GetSettingsAsync())
).RequireAuthorization("AdminOnly");

app.MapPut("/api/admin/settings", async (SettingsInput input, IAdminApi api) =>
    ToHttpResult(await api.SaveSettingsAsync(input))
).RequireAuthorization("AdminOnly");

// ---- Public settings: everything here is already shown on the public menu.
//      Needed by Editor users (e.g. currency display) who cannot read /api/admin. ----
app.MapGet("/api/public/settings", async (IAdminApi api) =>
    Results.Ok(await api.GetPublicSettingsAsync())
);

// Image upload: small images into the database, returns the public URL.
// Size/type are enforced inside IAdminApi; stored id is a random GUID, never
// the client-supplied filename. DisableAntiforgery per the docs: multipart
// calls from interactive components cannot provide the token; the
// SameSite=Lax cookie auth guards CSRF.
app.MapPost("/api/admin/images", async (IFormFile file, IAdminApi api) =>
{
    await using var ms = new MemoryStream();
    await file.OpenReadStream().CopyToAsync(ms);
    var result = await api.UploadImageAsync(ms.ToArray(), file.ContentType, file.FileName);
    return result is null
        ? Results.BadRequest(new { error = "Only images up to 2 MB are allowed." })
        : Results.Created(result.Url, result);
}).DisableAntiforgery().RequireAuthorization("EditorOrAdmin");

// ---- Public image serving: uploaded images and restaurant logo live in the
//      database (small files, per the Blazor file-uploads guidance). ----
app.MapGet("/images/{id:guid}", (Guid id, AppDbContext db) =>
{
    var image = db.ImageFiles.Find(id);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Data, image.ContentType);
});

// Apply migrations + seed (initial admin comes from configuration/env vars).
// Skipped during EF design-time tooling, which executes Main up to Run().
if (!EF.IsDesignTime)
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(
            db,
            builder.Configuration["Seed:AdminUsername"] ?? "",
            builder.Configuration["Seed:AdminPassword"] ?? "",
            builder.Configuration.GetValue("Seed:DemoData", true));
    }
}

app.Run();

// Maps the shared ApiResult onto an HTTP response for the JSON endpoints.
static IResult ToHttpResult(ApiResult result, string? createdUrl = null) =>
    result.Success
        ? (createdUrl is null ? Results.Ok() : Results.Created(createdUrl, result))
        : Results.BadRequest(new { error = result.Error });
