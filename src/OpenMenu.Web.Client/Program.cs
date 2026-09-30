using System.Globalization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using OpenMenu.Application.Shared;
using OpenMenu.Web.Client.Admin;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Deserializes the authentication state (name + roles) that the server
// serialized into the page during prerendering. Docs:
// https://learn.microsoft.com/aspnet/core/blazor/security/index
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddLocalization();

// Admin pages call the server's JSON admin APIs; the auth cookie flows
// automatically with same-origin requests.
builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// WebAssembly render path of the admin pages (the server circuit uses
// AdminApiServer). Docs: https://learn.microsoft.com/aspnet/core/blazor/call-web-api
builder.Services.AddScoped<IAdminApi, AdminApiClient>();

var host = builder.Build();

// InteractiveAuto pages re-render client-side once WASM attaches, which would
// reset IStringLocalizer to the browser locale (bug: UI flipped back to English
// after interactivity). Apply the .AspNetCore.Culture cookie before any
// component renders, mirroring the server's RequestLocalization. JS interop is
// required because cookies are not readable from WASM directly.
var js = host.Services.GetRequiredService<IJSRuntime>();
var cookie = await js.InvokeAsync<string?>("eval", "document.cookie");
var cultureValue = (cookie ?? string.Empty)
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .FirstOrDefault(c => c.StartsWith(".AspNetCore.Culture="))?[".AspNetCore.Culture=".Length..];
if (!string.IsNullOrEmpty(cultureValue))
{
    // Cookie value is URL-encoded (c=fa|uic=fa).
    var decoded = Uri.UnescapeDataString(cultureValue);
    var ui = decoded.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault(p => p.StartsWith("uic="))?[4..];
    if (!string.IsNullOrEmpty(ui))
    {
        var cultureInfo = SafeCulture(ui);
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
    }
}

static CultureInfo SafeCulture(string name)
{
    try { return new CultureInfo(name); }
    catch (CultureNotFoundException) { return new CultureInfo("en"); }
}

await host.RunAsync();
