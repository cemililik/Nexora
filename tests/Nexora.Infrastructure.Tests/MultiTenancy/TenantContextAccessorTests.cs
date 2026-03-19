using Nexora.Infrastructure.MultiTenancy;

namespace Nexora.Infrastructure.Tests.MultiTenancy;

public sealed class TenantContextAccessorTests
{
    [Fact]
    public void SetTenant_ShouldSetContext()
    {
        var accessor = new TenantContextAccessor();

        accessor.SetTenant("tenant-1", "org-1", "user-1");

        accessor.Current.TenantId.Should().Be("tenant-1");
        accessor.Current.SchemaName.Should().Be("tenant_tenant-1");
        accessor.Current.OrganizationId.Should().Be("org-1");
        accessor.Current.UserId.Should().Be("user-1");
    }

    [Fact]
    public void SetTenant_WithOnlyTenantId_ShouldSetDefaults()
    {
        var accessor = new TenantContextAccessor();

        accessor.SetTenant("tenant-2");

        accessor.Current.TenantId.Should().Be("tenant-2");
        accessor.Current.SchemaName.Should().Be("tenant_tenant-2");
        accessor.Current.OrganizationId.Should().BeNull();
        accessor.Current.UserId.Should().BeNull();
    }

    [Fact]
    public void Current_WithoutSetting_ShouldThrow()
    {
        var accessor = new TenantContextAccessor();

        var act = () => accessor.Current;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Tenant context not set*");
    }

    [Fact]
    public async Task SetTenant_ShouldBeAsyncLocalScoped()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant("main-tenant");

        string? innerTenantId = null;
        await Task.Run(() =>
        {
            // AsyncLocal flows to child tasks
            innerTenantId = accessor.Current.TenantId;
        });

        innerTenantId.Should().Be("main-tenant");
    }
}
