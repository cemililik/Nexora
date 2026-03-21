using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Nexora.SharedKernel.Abstractions.Secrets;
using Nexora.SharedKernel.Abstractions.Storage;

namespace Nexora.Infrastructure.Storage;

/// <summary>
/// MinIO/S3-compatible implementation of <see cref="IFileStorageService"/>.
/// Resolves credentials via <see cref="ISecretProvider"/> (Dapr Secret Store).
/// </summary>
public sealed class MinioFileStorageService(
    IOptions<MinioStorageOptions> options,
    ISecretProvider secretProvider,
    ILogger<MinioFileStorageService> logger) : IFileStorageService
{
    private IMinioClient? _client;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <inheritdoc />
    public async Task<PresignedUrlResult> GenerateUploadPresignedUrlAsync(
        string bucketName,
        string objectKey,
        string contentType,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        await EnsureBucketExistsAsync(client, bucketName, ct);

        var url = await client.PresignedPutObjectAsync(
            new PresignedPutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithExpiry((int)expiry.TotalSeconds));

        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);

        logger.LogDebug("Generated upload presigned URL for {BucketName}/{ObjectKey}, expires {ExpiresAt}",
            bucketName, objectKey, expiresAt);

        return new PresignedUrlResult(url, expiresAt);
    }

    /// <inheritdoc />
    public async Task<PresignedUrlResult> GenerateDownloadPresignedUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);

        var url = await client.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithExpiry((int)expiry.TotalSeconds));

        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);

        logger.LogDebug("Generated download presigned URL for {BucketName}/{ObjectKey}, expires {ExpiresAt}",
            bucketName, objectKey, expiresAt);

        return new PresignedUrlResult(url, expiresAt);
    }

    /// <inheritdoc />
    public async Task DeleteObjectAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);

        await client.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey), ct);

        logger.LogInformation("Deleted object {BucketName}/{ObjectKey}", bucketName, objectKey);
    }

    /// <inheritdoc />
    public async Task<bool> ObjectExistsAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);

        try
        {
            await client.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey), ct);
            return true;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return false;
        }
        catch (Minio.Exceptions.BucketNotFoundException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ObjectMetadataResult> GetObjectMetadataAsync(
        string bucketName,
        string objectKey,
        CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);

        var stat = await client.StatObjectAsync(
            new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey), ct);

        return new ObjectMetadataResult(
            stat.ContentType,
            stat.Size,
            stat.LastModified);
    }

    private async Task<IMinioClient> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null) return _client;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_client is not null) return _client;

            var opts = options.Value;
            var accessKey = await secretProvider.GetSecretAsync(opts.AccessKeySecret, ct);
            var secretKey = await secretProvider.GetSecretAsync(opts.SecretKeySecret, ct);

            var builder = new MinioClient()
                .WithEndpoint(opts.Endpoint)
                .WithCredentials(accessKey, secretKey);

            if (opts.UseSsl)
                builder.WithSSL();

            _client = builder.Build();

            logger.LogInformation("MinIO client initialized for endpoint {Endpoint}", opts.Endpoint);
            return _client;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task EnsureBucketExistsAsync(IMinioClient client, string bucketName, CancellationToken ct)
    {
        var exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName), ct);

        if (!exists)
        {
            await client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucketName), ct);
        }
    }
}
