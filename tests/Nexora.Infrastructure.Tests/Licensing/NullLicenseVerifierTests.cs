using Nexora.Infrastructure.Licensing;

namespace Nexora.Infrastructure.Tests.Licensing;

public sealed class NullLicenseVerifierTests
{
    [Fact]
    public async Task IsLicensedAsync_AnyTenantAndModule_ReturnsTrue()
    {
        // Arrange
        var verifier = new NullLicenseVerifier();

        // Act
        var result = await verifier.IsLicensedAsync(Guid.NewGuid(), "contacts");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsLicensedAsync_EmptyModuleName_ReturnsTrue()
    {
        // Arrange
        var verifier = new NullLicenseVerifier();

        // Act
        var result = await verifier.IsLicensedAsync(Guid.NewGuid(), string.Empty);

        // Assert
        result.Should().BeTrue();
    }
}
