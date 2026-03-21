using Nexora.SharedKernel.Abstractions.Storage;

namespace Nexora.Infrastructure.Tests.Storage;

public sealed class FileStorageResultTests
{
    [Fact]
    public void PresignedUrlResult_ValidResult_StoresProperties()
    {
        // Arrange
        var url = "https://minio.local/bucket/object?signature=abc";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        // Act
        var result = new PresignedUrlResult(url, expiresAt);

        // Assert
        result.Url.Should().Be(url);
        result.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void ObjectMetadataResult_ValidResult_StoresProperties()
    {
        // Arrange
        var contentType = "application/pdf";
        var size = 1024L;
        var lastModified = DateTimeOffset.UtcNow;

        // Act
        var result = new ObjectMetadataResult(contentType, size, lastModified);

        // Assert
        result.ContentType.Should().Be(contentType);
        result.Size.Should().Be(size);
        result.LastModified.Should().Be(lastModified);
    }

    [Fact]
    public void PresignedUrlResult_EqualInstances_AreEqual()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        var a = new PresignedUrlResult("https://url", expiresAt);
        var b = new PresignedUrlResult("https://url", expiresAt);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void ObjectMetadataResult_EqualInstances_AreEqual()
    {
        // Arrange
        var lastModified = DateTimeOffset.UtcNow;
        var a = new ObjectMetadataResult("application/pdf", 1024, lastModified);
        var b = new ObjectMetadataResult("application/pdf", 1024, lastModified);

        // Assert
        a.Should().Be(b);
    }
}
