using Nexora.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Tests.MultiTenancy;

public sealed class TenantSchemaManagerTests
{
    [Fact]
    public void ValidateSchemaName_EmptyName_ShouldThrow()
    {
        var manager = CreateManager();

        var act = () => manager.CreateSchemaAsync("", CancellationToken.None);

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ValidateSchemaName_WithoutTenantPrefix_ShouldThrow()
    {
        var manager = CreateManager();

        var act = () => manager.CreateSchemaAsync("bad_name", CancellationToken.None);

        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tenant_*");
    }

    [Fact]
    public void ValidateSchemaName_WithSpecialChars_ShouldThrow()
    {
        var manager = CreateManager();

        var act = () => manager.CreateSchemaAsync("tenant_; DROP TABLE", CancellationToken.None);

        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*invalid characters*");
    }

    private static TenantSchemaManager CreateManager()
    {
        // Use dummy connection string — validation tests don't hit the DB
        return new TenantSchemaManager(
            "Host=localhost;Database=test",
            [],
            Substitute.For<ILogger<TenantSchemaManager>>());
    }
}
