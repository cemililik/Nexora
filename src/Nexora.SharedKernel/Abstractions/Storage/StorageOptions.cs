namespace Nexora.SharedKernel.Abstractions.Storage;

/// <summary>
/// Application-level storage configuration consumed by module handlers.
/// Infrastructure-specific options (endpoint, credentials) remain in <c>Nexora.Infrastructure</c>.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Storage:Minio";

    /// <summary>Default bucket prefix. Tenant-specific buckets follow the pattern: {BucketPrefix}-{tenantId}.</summary>
    public string BucketPrefix { get; set; } = "nexora";

    /// <summary>Default presigned URL expiry duration.</summary>
    public TimeSpan DefaultPresignedUrlExpiry { get; set; } = TimeSpan.FromMinutes(15);
}
