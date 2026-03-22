namespace Nexora.SharedKernel.Abstractions.Storage;

/// <summary>
/// Abstraction for object/file storage operations (MinIO, S3-compatible).
/// Provides presigned URL generation for secure, direct client uploads/downloads.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Generates a presigned URL for uploading a file directly from the client.</summary>
    /// <param name="bucketName">Target bucket name.</param>
    /// <param name="objectKey">Object key (path) within the bucket.</param>
    /// <param name="contentType">Expected MIME type of the upload.</param>
    /// <param name="expiry">URL validity duration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A presigned upload URL result.</returns>
    Task<PresignedUrlResult> GenerateUploadPresignedUrlAsync(
        string bucketName,
        string objectKey,
        string contentType,
        TimeSpan expiry,
        CancellationToken ct = default);

    /// <summary>Generates a presigned URL for downloading a file directly from the client.</summary>
    /// <param name="bucketName">Source bucket name.</param>
    /// <param name="objectKey">Object key (path) within the bucket.</param>
    /// <param name="expiry">URL validity duration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A presigned download URL result.</returns>
    Task<PresignedUrlResult> GenerateDownloadPresignedUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan expiry,
        CancellationToken ct = default);

    /// <summary>Deletes an object from storage.</summary>
    /// <param name="bucketName">Bucket containing the object.</param>
    /// <param name="objectKey">Object key to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteObjectAsync(string bucketName, string objectKey, CancellationToken ct = default);

    /// <summary>Checks whether an object exists in the specified bucket.</summary>
    /// <param name="bucketName">Bucket to check.</param>
    /// <param name="objectKey">Object key to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the object exists; otherwise false.</returns>
    Task<bool> ObjectExistsAsync(string bucketName, string objectKey, CancellationToken ct = default);

    /// <summary>Downloads the raw content of an object from storage.</summary>
    /// <param name="bucketName">Bucket containing the object.</param>
    /// <param name="objectKey">Object key to download.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw byte content of the object.</returns>
    Task<byte[]> GetObjectAsync(string bucketName, string objectKey, CancellationToken ct = default);

    /// <summary>Gets metadata for an existing object.</summary>
    /// <param name="bucketName">Bucket containing the object.</param>
    /// <param name="objectKey">Object key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Object metadata including content type and size.</returns>
    Task<ObjectMetadataResult> GetObjectMetadataAsync(string bucketName, string objectKey, CancellationToken ct = default);
}
