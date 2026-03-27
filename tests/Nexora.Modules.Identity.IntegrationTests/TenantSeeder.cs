using System.Reflection;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;

namespace Nexora.Modules.Identity.IntegrationTests;

/// <summary>
/// Shared helper for seeding a Tenant entity in integration tests.
/// Uses reflection to set the Id property on the base Entity type,
/// since the domain model does not expose a public setter.
/// </summary>
internal static class TenantSeeder
{
    public static void SeedTenant(
        PlatformDbContext platformDb, TenantId tenantId, string name, string slug, string realmId)
    {
        var tenant = Tenant.Create(name, slug);

        // Use safer PropertyInfo lookup that searches Instance + Public + NonPublic,
        // instead of fragile BaseType!.BaseType! chain that breaks if the hierarchy changes.
        var idProperty = typeof(Tenant).GetProperty(
            "Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        idProperty!.SetValue(tenant, tenantId);
        tenant.SetRealmId(realmId);
        platformDb.Tenants.Add(tenant);
        platformDb.SaveChanges();
    }
}
