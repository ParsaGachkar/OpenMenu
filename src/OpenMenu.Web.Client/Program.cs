using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OpenMenu.Domain;
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

await builder.Build().RunAsync();
