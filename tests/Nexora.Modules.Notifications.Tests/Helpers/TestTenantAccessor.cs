using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Helpers;

/// <summary>
/// Creates a pre-configured <see cref="ITenantContextAccessor"/> for use in tests.
/// </summary>
internal static class TestTenantAccessor
{
    public static ITenantContextAccessor Create(Guid tenantId, Guid orgId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.ToString(), orgId.ToString());
        return accessor;
    }
}
