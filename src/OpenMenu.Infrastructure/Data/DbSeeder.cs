using Microsoft.EntityFrameworkCore;
using OpenMenu.Domain;
using OpenMenu.Infrastructure.Auth;

namespace OpenMenu.Infrastructure.Data;

/// <summary>
/// Seeds the initial admin user (credentials from configuration, never hardcoded),
/// restaurant settings and demo menu data. Idempotent.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, string adminUsername, string adminPassword, bool seedDemoData)
    {
        if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return; // Nothing to seed.
        }

        var adminExists = await db.Users.AnyAsync(u => u.Username == adminUsername);
        if (!adminExists)
        {
            db.Users.Add(new User
            {
                Username = adminUsername,
                PasswordHash = PasswordHasher.Hash(adminPassword),
                Role = UserRole.Admin,
                IsActive = true,
            });
        }

        var settingsExist = await db.RestaurantSettings.AnyAsync();
        if (!settingsExist)
        {
            db.RestaurantSettings.Add(new RestaurantSettings
            {
                Id = 1,
                Name = "My Restaurant",
                Description = "A small restaurant serving fresh food every day.",
                Currency = "USD",
                Theme = "light",
            });
        }

        if (seedDemoData && !await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category
                {
                    Name = "Appetizers",
                    Description = "Small bites to start your meal",
                    SortOrder = 1,
                    Items =
                    [
                        new MenuItem { Name = "Hummus with Pita", Description = "Creamy chickpea dip with warm pita", Price = 6.50m, SortOrder = 1 },
                        new MenuItem { Name = "Falafel Balls", Description = "Six crispy falafel with tahini sauce", Price = 7.00m, SortOrder = 2 },
                    ],
                },
                new Category
                {
                    Name = "Main Courses",
                    Description = "Hearty plates for a full meal",
                    SortOrder = 2,
                    Items =
                    [
                        new MenuItem { Name = "Grilled Chicken Plate", Description = "Chicken skewers, saffron rice, grilled tomato", Price = 15.90m, SortOrder = 1 },
                        new MenuItem { Name = "Lamb Kebab", Description = "Marinated lamb, flatbread, mint yogurt", Price = 18.50m, SortOrder = 2 },
                        new MenuItem { Name = "Vegetable Curry", Description = "Seasonal vegetables in coconut curry with rice", Price = 12.00m, SortOrder = 3, IsAvailable = false },
                    ],
                },
                new Category
                {
                    Name = "Desserts & Drinks",
                    Description = "Something sweet and refreshing",
                    SortOrder = 3,
                    Items =
                    [
                        new MenuItem { Name = "Baklava", Description = "Pistachio baklava, three pieces", Price = 5.50m, SortOrder = 1 },
                        new MenuItem { Name = "Fresh Lemonade", Description = "House-made, lightly sweetened", Price = 3.00m, SortOrder = 2 },
                    ],
                });
        }

        await db.SaveChangesAsync();
    }
}
