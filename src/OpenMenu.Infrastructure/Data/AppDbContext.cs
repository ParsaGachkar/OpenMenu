using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenMenu.Domain;

namespace OpenMenu.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RestaurantSettings> RestaurantSettings => Set<RestaurantSettings>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<ImageFile> ImageFiles => Set<ImageFile>();
    public DbSet<MenuItemTranslation> MenuItemTranslations => Set<MenuItemTranslation>();
    public DbSet<CategoryTranslation> CategoryTranslations => Set<CategoryTranslation>();
    public DbSet<MenuItemImage> MenuItemImages => Set<MenuItemImage>();
    public DbSet<MenuItemVideo> MenuItemVideos => Set<MenuItemVideo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
    }
}

public class RestaurantSettingsConfiguration : IEntityTypeConfiguration<RestaurantSettings>
{
    public void Configure(EntityTypeBuilder<RestaurantSettings> builder)
    {
        builder.ToTable("RestaurantSettings");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.LogoUrl).HasMaxLength(500);
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.Theme).HasMaxLength(50).IsRequired();
        builder.Property(r => r.PrimaryColor).HasMaxLength(9);
        builder.Property(r => r.DefaultCulture).HasMaxLength(10).IsRequired();
        builder.Property(r => r.EnabledCultures).HasMaxLength(200).IsRequired();

        // Single-row table: fixed key, never database-generated.
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.ToTable(t => t.HasCheckConstraint("CK_RestaurantSettings_SingleRow", "\"Id\" = 1"));
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);

        builder.HasIndex(c => c.SortOrder);
    }
}

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(2000);
        builder.Property(m => m.ImageUrl).HasMaxLength(500);
        builder.Property(m => m.Price).HasPrecision(12, 2);

        builder.HasOne(m => m.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.CategoryId, m.SortOrder });
    }
}

public class MenuItemTranslationConfiguration : IEntityTypeConfiguration<MenuItemTranslation>
{
    public void Configure(EntityTypeBuilder<MenuItemTranslation> builder)
    {
        builder.ToTable("MenuItemTranslations");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Culture).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Price).HasPrecision(12, 2);

        builder.HasOne(t => t.MenuItem)
            .WithMany(m => m.Translations)
            .HasForeignKey(t => t.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // One translation row per culture per item.
        builder.HasIndex(t => new { t.MenuItemId, t.Culture }).IsUnique();
    }
}

public class CategoryTranslationConfiguration : IEntityTypeConfiguration<CategoryTranslation>
{
    public void Configure(EntityTypeBuilder<CategoryTranslation> builder)
    {
        builder.ToTable("CategoryTranslations");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Culture).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(2000);

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Translations)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // One translation row per culture per category.
        builder.HasIndex(t => new { t.CategoryId, t.Culture }).IsUnique();
    }
}

public class MenuItemImageConfiguration : IEntityTypeConfiguration<MenuItemImage>
{
    public void Configure(EntityTypeBuilder<MenuItemImage> builder)
    {
        builder.ToTable("MenuItemImages");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Url).HasMaxLength(500).IsRequired();

        builder.HasOne(i => i.MenuItem)
            .WithMany(m => m.Images)
            .HasForeignKey(i => i.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.MenuItemId, i.SortOrder });
    }
}

public class MenuItemVideoConfiguration : IEntityTypeConfiguration<MenuItemVideo>
{
    public void Configure(EntityTypeBuilder<MenuItemVideo> builder)
    {
        builder.ToTable("MenuItemVideos");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Url).HasMaxLength(500).IsRequired();
        builder.Property(v => v.ContentType).HasMaxLength(100);

        // One optional video per menu item.
        builder.HasOne(v => v.MenuItem)
            .WithOne(m => m.Video)
            .HasForeignKey<MenuItemVideo>(v => v.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ImageFileConfiguration : IEntityTypeConfiguration<ImageFile>
{
    public void Configure(EntityTypeBuilder<ImageFile> builder)
    {
        builder.ToTable("ImageFiles");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.ContentType).HasMaxLength(100).IsRequired();
    }
}
