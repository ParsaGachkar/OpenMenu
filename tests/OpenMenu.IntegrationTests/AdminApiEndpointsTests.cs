using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenMenu.Application.Shared;
using OpenMenu.Domain;
using OpenMenu.Infrastructure.Data;

namespace OpenMenu.IntegrationTests;

/// <summary>
/// The admin JSON APIs against the real host: create/read/update round trips,
/// authorization, and the culture-set rules. PostgreSQL is mocked away with
/// in-memory SQLite — no external dependency — while routing, model binding,
/// auth policies and EF mapping behavior stay real.
/// </summary>
public class AdminApiEndpointsTests : IClassFixture<OpenMenuFactory>, IAsyncLifetime
{
    private readonly OpenMenuFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _anonymous;

    public AdminApiEndpointsTests(OpenMenuFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateAdminClient();
        _anonymous = factory.CreateAnonymousClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Category_RoundTrip_CreateUpdateDelete()
    {
        // Create (201 Created with the new id in the body).
        var create = await _admin.PostAsJsonAsync("/api/admin/categories",
            new CategoryInput($"IT Cat {Guid.NewGuid():N}".Substring(0, 14), "desc", 1, true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdId = (await create.Content.ReadFromJsonAsync<ApiResult>())!.CreatedId;
        Assert.True(createdId is > 0);

        // Read.
        var categories = await _admin.GetFromJsonAsync<CategoryDto[]>("/api/admin/categories");
        Assert.Contains(categories!, c => c.Id == createdId);

        // Update.
        var update = await _admin.PutAsJsonAsync($"/api/admin/categories/{createdId}",
            new CategoryInput("renamed", "desc2", 2, false));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cat = await db.Categories.AsNoTracking().FirstAsync(c => c.Id == createdId);
            Assert.Equal("renamed", cat.Name);
            Assert.False(cat.IsVisible);
        }

        // Delete.
        var delete = await _admin.PostAsJsonAsync($"/api/admin/categories/{createdId}/delete", new { });
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        var after = await _admin.GetFromJsonAsync<CategoryDto[]>("/api/admin/categories");
        Assert.DoesNotContain(after!, c => c.Id == createdId);
    }

    [Fact]
    public async Task MenuItem_WithTranslation_PersistsPerCultureContent()
    {
        var categoryId = await CreateCategoryAsync();

        var create = await _admin.PostAsJsonAsync("/api/admin/menu-items",
            new MenuItemInput(categoryId, "IT Item", "desc", 9.90m, null, 1, true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var itemId = (await create.Content.ReadFromJsonAsync<ApiResult>())!.CreatedId!.Value;

        // Per-culture translation round trip.
        var save = await _admin.PutAsJsonAsync($"/api/admin/menu-items/{itemId}/translations",
            new MenuItemTranslationInput("fa", "آیتم", "توضیح", 99000m));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var translations = await _admin.GetFromJsonAsync<MenuItemTranslation[]>(
            $"/api/admin/menu-items/{itemId}/translations");
        var fa = Assert.Single(translations!, t => t.Culture == "fa");
        Assert.Equal("آیتم", fa.Name);
        Assert.Equal(99000m, fa.Price);

        // Cleanup.
        await _admin.PostAsJsonAsync($"/api/admin/menu-items/{itemId}/delete", new { });
        await _admin.PostAsJsonAsync($"/api/admin/categories/{categoryId}/delete", new { });
    }

    [Fact]
    public async Task AdminEndpoints_RequireAuthorization()
    {
        var response = await _anonymous.GetAsync("/api/admin/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CultureSet_RejectsDisabledCultures()
    {
        // ar is not in the default enabled set (en,fa,tr,ar seeded — use a
        // definitely-disabled culture code instead).
        var response = await _anonymous.GetAsync("/culture/set?culture=xx&redirectTo=/");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PublicSettings_ExposesCultureConfiguration()
    {
        var response = await _anonymous.GetAsync("/api/public/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(json.RootElement.TryGetProperty("defaultCulture", out _));
        Assert.True(json.RootElement.TryGetProperty("enabledCultures", out _));
    }

    private async Task<int> CreateCategoryAsync()
    {
        var create = await _admin.PostAsJsonAsync("/api/admin/categories",
            new CategoryInput($"IT Cat {Guid.NewGuid():N}".Substring(0, 14), null, 1, true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<ApiResult>())!.CreatedId!.Value;
    }
}
