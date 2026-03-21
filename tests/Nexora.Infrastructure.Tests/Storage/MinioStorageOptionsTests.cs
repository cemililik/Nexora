using Nexora.Infrastructure.Storage;

namespace Nexora.Infrastructure.Tests.Storage;

public sealed class MinioStorageOptionsTests
{
    [Fact]
    public void Defaults_NewInstance_HasExpectedValues()
    {
        // Arrange & Act
        var options = new MinioStorageOptions();

        // Assert
        options.Endpoint.Should().Be("localhost:9000");
        options.UseSsl.Should().BeFalse();
        options.BucketPrefix.Should().Be("nexora");
        options.AccessKeySecret.Should().Be("nexora/minio/access-key");
        options.SecretKeySecret.Should().Be("nexora/minio/secret-key");
        options.DefaultPresignedUrlExpiry.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void SectionName_Always_IsStorageMinio()
    {
        MinioStorageOptions.SectionName.Should().Be("Storage:Minio");
    }

    [Fact]
    public void Properties_SetValues_AreSettable()
    {
        // Arrange & Act
        var options = new MinioStorageOptions
        {
            Endpoint = "minio.prod:9000",
            UseSsl = true,
            BucketPrefix = "custom",
            AccessKeySecret = "custom/access",
            SecretKeySecret = "custom/secret",
            DefaultPresignedUrlExpiry = TimeSpan.FromMinutes(30)
        };

        // Assert
        options.Endpoint.Should().Be("minio.prod:9000");
        options.UseSsl.Should().BeTrue();
        options.BucketPrefix.Should().Be("custom");
        options.AccessKeySecret.Should().Be("custom/access");
        options.SecretKeySecret.Should().Be("custom/secret");
        options.DefaultPresignedUrlExpiry.Should().Be(TimeSpan.FromMinutes(30));
    }
}
