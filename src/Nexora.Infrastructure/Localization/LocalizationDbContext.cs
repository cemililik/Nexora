using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Localization.Entities;

namespace Nexora.Infrastructure.Localization;

/// <summary>
/// DbContext for platform-wide localization tables (public schema).
/// NOT tenant-scoped — translations are shared across all tenants,
/// with optional per-tenant overrides.
/// </summary>
public sealed class LocalizationDbContext(
    DbContextOptions<LocalizationDbContext> options) : DbContext(options)
{
    public DbSet<LocalizationResource> Resources => Set<LocalizationResource>();
    public DbSet<LocalizationOverride> Overrides => Set<LocalizationOverride>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<LocalizationResource>(e =>
        {
            e.ToTable("localization_resources");
            e.HasKey(r => r.Id);

            e.Property(r => r.LanguageCode).HasMaxLength(10).IsRequired();
            e.Property(r => r.Key).HasMaxLength(255).IsRequired();
            e.Property(r => r.Value).IsRequired();
            e.Property(r => r.Module).HasMaxLength(50);
            e.Property(r => r.UpdatedAt);

            e.HasIndex(r => new { r.LanguageCode, r.Key }).IsUnique();
            e.HasIndex(r => new { r.LanguageCode, r.Module });
        });

        modelBuilder.Entity<LocalizationOverride>(e =>
        {
            e.ToTable("localization_overrides");
            e.HasKey(o => o.Id);

            e.Property(o => o.TenantId).IsRequired();
            e.Property(o => o.LanguageCode).HasMaxLength(10).IsRequired();
            e.Property(o => o.Key).HasMaxLength(255).IsRequired();
            e.Property(o => o.Value).IsRequired();

            e.HasIndex(o => new { o.TenantId, o.LanguageCode, o.Key }).IsUnique();
        });
    }
}
