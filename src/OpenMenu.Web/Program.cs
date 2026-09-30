using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using OpenMenu.Domain;
using OpenMenu.Application.Shared;
using OpenMenu.Infrastructure;
using OpenMenu.Application.Admin;
using OpenMenu.Infrastructure.Data;
using OpenMenu.Web.Components;
using QRCoder;
using OpenMenu.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents()
        .AddInteractiveWebAssemblyComponents()
        .AddAuthenticationStateSerialization();

// Videos up to 50 MB are uploaded through the JSON/multipart endpoints;
// Kestrel's default 30 MB request body cap would kill them mid-flight.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52_428_800; // 50 MB + overhead
});

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
// falling back to Accept-Language and then the restaurant's configured default
// culture. Placed per docs immediately after static files, before any
// component/endpoints rendering. The default is read from the single-row
// RestaurantSettings (feature: admin-configurable default culture); an empty
// database falls back to "en" so first boot still renders.
string defaultCulture;
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Migrations must run before anything reads the new columns (the crash on
    // boot in the first deploy of this feature was exactly this ordering bug).
    // Integration tests swap the provider for in-memory SQLite (no external DB)
    // and set Database:EnsureCreated — Npgsql migrations don't apply there.
    if (!EF.IsDesignTime)
    {
        if (builder.Configuration.GetValue("Database:EnsureCreated", false))
        {
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            await db.Database.MigrateAsync();
        }
        await DbSeeder.SeedAsync(
            db,
            builder.Configuration["Seed:AdminUsername"] ?? "",
            builder.Configuration["Seed:AdminPassword"] ?? "",
            builder.Configuration.GetValue("Seed:DemoData", true));
    }

    defaultCulture = await db.RestaurantSettings.AsNoTracking()
        .Select(s => s.DefaultCulture).FirstOrDefaultAsync() ?? "en";
}
if (!SupportedCultures.IsValid(defaultCulture))
{
    defaultCulture = "en";
}

var supportedCultures = SupportedCultures.All;
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(defaultCulture)
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
// only writes a preference cookie, never data. Only cultures the restaurant
// enabled are accepted (feature: enable/disable cultures).
app.MapGet("/culture/set", async (string culture, string? redirectTo, HttpContext ctx, AppDbContext db) =>
{
    var settings = await db.RestaurantSettings.AsNoTracking().FirstOrDefaultAsync();
    var enabled = SupportedCultures.SplitEnabled(settings?.EnabledCultures, settings?.DefaultCulture ?? "en");
    if (!enabled.Contains(SupportedCultures.Normalize(culture), StringComparer.OrdinalIgnoreCase))
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

// Per-culture content translations (feature: localized menu).
app.MapGet("/api/admin/categories/{id:int}/translations", async (int id, IAdminApi api) =>
    Results.Ok(await api.GetCategoryTranslationsAsync(id))
).RequireAuthorization("EditorOrAdmin");

app.MapPut("/api/admin/categories/{id:int}/translations", async (int id, CategoryTranslationInput input, IAdminApi api) =>
    ToHttpResult(await api.SaveCategoryTranslationAsync(id, input))
).RequireAuthorization("EditorOrAdmin");

app.MapGet("/api/admin/menu-items/{id:int}/translations", async (int id, IAdminApi api) =>
    Results.Ok(await api.GetMenuItemTranslationsAsync(id))
).RequireAuthorization("EditorOrAdmin");

app.MapPut("/api/admin/menu-items/{id:int}/translations", async (int id, MenuItemTranslationInput input, IAdminApi api) =>
    ToHttpResult(await api.SaveMenuItemTranslationAsync(id, input))
).RequireAuthorization("EditorOrAdmin");

// Multi-image gallery (feature: multiple images per menu item).
app.MapGet("/api/admin/menu-items/{id:int}/images", async (int id, IAdminApi api) =>
    Results.Ok(await api.GetMenuItemImagesAsync(id))
).RequireAuthorization("EditorOrAdmin");

app.MapPost("/api/admin/menu-items/{id:int}/images", async (int id, MenuItemImageInput input, IAdminApi api) =>
    ToHttpResult(await api.AddMenuItemImageAsync(id, input.Url))
).RequireAuthorization("EditorOrAdmin");

app.MapPost("/api/admin/menu-items/images/{imageId:int}/delete", async (int imageId, IAdminApi api) =>
    ToHttpResult(await api.DeleteMenuItemImageAsync(imageId))
).RequireAuthorization("EditorOrAdmin");

// Per-item video (feature: video support). Null body removes the video.
app.MapGet("/api/admin/menu-items/{id:int}/video", async (int id, IAdminApi api) =>
    Results.Ok(await api.GetMenuItemVideoAsync(id)) // null body (not 204): GetFromJsonAsync throws on empty
).RequireAuthorization("EditorOrAdmin");

app.MapPut("/api/admin/menu-items/{id:int}/video", async (int id, MenuItemVideoInput? input, IAdminApi api) =>
    ToHttpResult(await api.SetMenuItemVideoAsync(id, input))
).RequireAuthorization("EditorOrAdmin");

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

// Video upload: stored on the mounted media volume (never in the database —
// large blobs belong on disk); the DB keeps only the MenuItemVideo metadata row.
app.MapPost("/api/admin/videos", async (IFormFile file, IAdminApi api) =>
{
    await using var ms = new MemoryStream();
    await file.OpenReadStream().CopyToAsync(ms);
    var result = await api.UploadVideoAsync(ms.ToArray(), file.ContentType, file.FileName);
    return result is null
        ? Results.BadRequest(new { error = "Only videos up to 50 MB are allowed." })
        : Results.Created(result.Url, result);
})
.DisableAntiforgery().RequireAuthorization("EditorOrAdmin");

// ---- Public media serving: uploaded videos live on the media volume. Range
//      requests are enabled so the browser can seek. ----
app.MapGet("/media/videos/{name}", (string name) =>
{
    // Random-GUID names only; no path traversal.
    if (name.Contains('/') || name.Contains("..") || !Guid.TryParse(Path.GetFileNameWithoutExtension(name), out _))
    {
        return Results.NotFound();
    }

    var path = Path.Combine(AppContext.BaseDirectory, "media", "videos", name);
    if (!File.Exists(path))
    {
        return Results.NotFound();
    }

    // Content type from the extension the upload stored, not a hard-coded
    // mp4: a webm served as video/mp4 will not play in some browsers.
    var contentType = Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".webm" => "video/webm",
        ".ogv" => "video/ogg",
        ".mov" => "video/quicktime",
        ".m4v" => "video/x-m4v",
        _ => "video/mp4",
    };
    return Results.File(path, contentType, enableRangeProcessing: true);
});

// ---- QR code (feature): PNG of the public menu URL for printing/table
// tents. Computed from the request (host + path base) so it stays correct on
// any deployment domain without extra configuration.
app.MapGet("/qr/menu", (HttpRequest request) =>
{
    var menuUrl = $"{request.Scheme}://{request.Host}";
    var pathBase = request.PathBase.Value;
    if (!string.IsNullOrEmpty(pathBase))
    {
        menuUrl += pathBase;
    }

    using var generator = new QRCodeGenerator();
    using var data = generator.CreateQrCode(menuUrl, QRCodeGenerator.ECCLevel.M);
    var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 8);
    return Results.File(png, "image/png");
});

// ---- Public image serving: uploaded images and restaurant logo live in the
//      database (small files, per the Blazor file-uploads guidance). ----
app.MapGet("/images/{id:guid}", (Guid id, AppDbContext db) =>
{
    var image = db.ImageFiles.Find(id);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Data, image.ContentType);
});

app.Run();

// Maps the shared ApiResult onto an HTTP response for the JSON endpoints.
static IResult ToHttpResult(ApiResult result, string? createdUrl = null) =>
    result.Success
        ? (createdUrl is null ? Results.Ok() : Results.Created(createdUrl, result))
        : Results.BadRequest(new { error = result.Error });

// Exposes the implicitly generated Program class to test projects
// (WebApplicationFactory<Program>).
public partial class Program;
