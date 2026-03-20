// NOTE: This helper isolates the direct dependency on Nexora.Infrastructure.MultiTenancy.TenantContextAccessor
// so that all test files can use ITenantContextAccessor without importing the Infrastructure layer directly.
// Ideally, a lightweight fake implementation should replace this to fully decouple tests from Infrastructure.
// See: https://github.com/nexora/nexora/issues/XXX (track removal of Infrastructure dependency from unit tests)
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
