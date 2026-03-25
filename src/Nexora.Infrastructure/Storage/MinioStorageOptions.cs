namespace Nexora.Infrastructure.Storage;

/// <summary>
/// Configuration options for MinIO/S3-compatible object storage.
/// Secrets (AccessKey, SecretKey) are resolved via <see cref="Nexora.SharedKernel.Abstractions.Secrets.ISecretProvider"/>.
/// </summary>
public sealed class MinioStorageOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Storage:Minio";

    /// <summary>MinIO server endpoint (e.g., "minio:9000").</summary>
    public string Endpoint { get; set; } = "localhost:9000";

    /// <summary>Public-facing endpoint for presigned URLs that browsers can reach (e.g., "localhost:9000").</summary>
    public string? PublicEndpoint { get; set; }

    /// <summary>Whether to use SSL for MinIO connections.</summary>
    public bool UseSsl { get; set; }

    /// <summary>Default bucket prefix. Tenant-specific buckets follow the pattern: {BucketPrefix}-{tenantId}.</summary>
    public string BucketPrefix { get; set; } = "nexora";

    /// <summary>Dapr secret store key for the MinIO access key.</summary>
    public string AccessKeySecret { get; set; } = "nexora/minio/access-key";

    /// <summary>Dapr secret store key for the MinIO secret key.</summary>
    public string SecretKeySecret { get; set; } = "nexora/minio/secret-key";

    /// <summary>Default presigned URL expiry duration.</summary>
    public TimeSpan DefaultPresignedUrlExpiry { get; set; } = TimeSpan.FromMinutes(15);
}
