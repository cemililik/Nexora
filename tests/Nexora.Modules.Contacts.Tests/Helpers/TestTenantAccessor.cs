// NOTE: This helper isolates the direct dependency on Nexora.Infrastructure.MultiTenancy.TenantContextAccessor
// so that all test files can use ITenantContextAccessor without importing the Infrastructure layer directly.
// A lightweight fake implementation could replace this to fully decouple tests from Infrastructure; not
// pursued yet because the real TenantContextAccessor is trivial (AsyncLocal holder) and wrapping it in a
// single helper type is enough for the current test surface.
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Tests.Helpers;

/// <summary>
/// Creates a pre-configured <see cref="ITenantContextAccessor"/> for use in tests.
/// Centralises the Infrastructure dependency so it can be replaced with a fake in one place.
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
