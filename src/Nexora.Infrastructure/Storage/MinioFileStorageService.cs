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
    private IMinioClient? _publicClient;
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

        var publicClient = await GetPublicClientAsync(ct);
        var url = await publicClient.PresignedPutObjectAsync(
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
        await GetClientAsync(ct);

        var publicClient = await GetPublicClientAsync(ct);
        var url = await publicClient.PresignedGetObjectAsync(
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
    public async Task UploadObjectAsync(
        string bucketName,
        string objectKey,
        byte[] data,
        string contentType,
        CancellationToken ct = default)
    {
        using var stream = new MemoryStream(data);
        await UploadObjectAsync(bucketName, objectKey, stream, contentType, ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The stream must be seekable. Position is reset to 0 before upload to ensure
    /// all content is uploaded regardless of the caller's stream position.
    /// </remarks>
    public async Task UploadObjectAsync(
        string bucketName,
        string objectKey,
        Stream stream,
        string contentType,
        CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        await EnsureBucketExistsAsync(client, bucketName, ct);

        // If the stream is not seekable, buffer it into a MemoryStream
        Stream uploadStream = stream;
        MemoryStream? buffered = null;
        if (!stream.CanSeek)
        {
            buffered = new MemoryStream();
            await stream.CopyToAsync(buffered, ct);
            buffered.Position = 0;
            uploadStream = buffered;
        }
        else
        {
            uploadStream.Position = 0;
        }

        try
        {
            await client.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey)
                    .WithStreamData(uploadStream)
                    .WithObjectSize(uploadStream.Length)
                    .WithContentType(contentType), ct);

            logger.LogInformation("Uploaded object {BucketName}/{ObjectKey} ({Size} bytes)",
                bucketName, objectKey, uploadStream.Length);
        }
        finally
        {
            if (buffered is not null)
                await buffered.DisposeAsync();
        }
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
    public async Task<byte[]> GetObjectAsync(
        string bucketName,
        string objectKey,
        CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);

        using var memoryStream = new MemoryStream();
        await client.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithCallbackStream(async (stream, cancellationToken) =>
                    await stream.CopyToAsync(memoryStream, cancellationToken)), ct);

        logger.LogDebug("Downloaded object {BucketName}/{ObjectKey} ({Size} bytes)",
            bucketName, objectKey, memoryStream.Length);

        return memoryStream.ToArray();
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

            _client = BuildClient(opts.Endpoint, accessKey, secretKey, opts.UseSsl);

            if (!string.IsNullOrEmpty(opts.PublicEndpoint) && opts.PublicEndpoint != opts.Endpoint)
                _publicClient = BuildClient(opts.PublicEndpoint, accessKey, secretKey, opts.UseSsl);

            logger.LogInformation("MinIO client initialized for endpoint {Endpoint}", opts.Endpoint);
            return _client;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Returns the public-facing client for presigned URL generation, or falls back to the internal client.
    /// </summary>
    private async Task<IMinioClient> GetPublicClientAsync(CancellationToken ct)
    {
        await GetClientAsync(ct);
        return _publicClient ?? _client!;
    }

    private static IMinioClient BuildClient(string endpoint, string accessKey, string secretKey, bool useSsl)
    {
        var builder = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey);

        if (useSsl)
            builder.WithSSL();

        return builder.Build();
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
