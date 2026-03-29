using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Infrastructure.Persistence;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Persistence;

/// <summary>
/// Concrete DbContext subclass for testing TenantModelCacheKeyFactory.
/// </summary>
internal sealed class CacheTestDbContext(
    DbContextOptions<CacheTestDbContext> options,
    ITenantContextAccessor tenantContextAccessor) : BaseDbContext(options, tenantContextAccessor);

public sealed class TenantModelCacheKeyFactoryTests
{
    private readonly TenantModelCacheKeyFactory _factory = new();

    private static CacheTestDbContext CreateDbContext(ITenantContextAccessor accessor)
    {
        var options = new DbContextOptionsBuilder<CacheTestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CacheTestDbContext(options, accessor);
    }

    [Fact]
    public void Create_WithBaseDbContext_IncludesSchemaInKey()
    {
        // Arrange
        var accessor = new TenantContextAccessor();
        accessor.SetTenant("tenant-1");

        using var context = CreateDbContext(accessor);

        // Act
        var key = _factory.Create(context, designTime: false);

        // Assert
        var tuple = key.Should().BeOfType<(Type, string, bool)>().Subject;
        tuple.Item2.Should().Be("tenant_tenant-1");
        tuple.Item3.Should().BeFalse();
    }

    [Fact]
    public void Create_WithDifferentSchemas_ProducesDifferentKeys()
    {
        // Arrange + Act: get key1 with tenant-a
        var accessor = new TenantContextAccessor();
        accessor.SetTenant("tenant-a");
        using var context1 = CreateDbContext(accessor);
        var key1 = _factory.Create(context1, designTime: false);

        // Change tenant to tenant-b and create a new context
        accessor.SetTenant("tenant-b");
        using var context2 = CreateDbContext(accessor);
        var key2 = _factory.Create(context2, designTime: false);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Create_WithNoTenantContext_UsesDefaultSchema()
    {
        // Arrange — accessor that throws when Current is accessed (no tenant set)
        var accessor = new TenantContextAccessor();
        using var context = CreateDbContext(accessor);

        // Act
        var key = _factory.Create(context, designTime: false);

        // Assert
        var tuple = key.Should().BeOfType<(Type, string, bool)>().Subject;
        tuple.Item2.Should().Be("default");
    }

    [Fact]
    public void Create_DesignTime_IncludedInKey()
    {
        // Arrange
        var accessor = new TenantContextAccessor();
        accessor.SetTenant("tenant-1");

        using var context = CreateDbContext(accessor);

        // Act
        var keyDesignTime = _factory.Create(context, designTime: true);
        var keyRuntime = _factory.Create(context, designTime: false);

        // Assert
        keyDesignTime.Should().NotBe(keyRuntime);
        var tupleDesign = keyDesignTime.Should().BeOfType<(Type, string, bool)>().Subject;
        tupleDesign.Item3.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNonBaseDbContext_UsesDefaultSchema()
    {
        // Arrange — a plain DbContext (not BaseDbContext)
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new DbContext(options);

        // Act
        var key = _factory.Create(context, designTime: false);

        // Assert
        var tuple = key.Should().BeOfType<(Type, string, bool)>().Subject;
        tuple.Item2.Should().Be("default");
    }
}
