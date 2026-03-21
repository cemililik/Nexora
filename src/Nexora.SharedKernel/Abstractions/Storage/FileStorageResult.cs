namespace Nexora.SharedKernel.Abstractions.Storage;

/// <summary>Result of a presigned URL generation operation.</summary>
/// <param name="Url">The presigned URL for upload or download.</param>
/// <param name="ExpiresAt">UTC timestamp when the URL expires.</param>
public sealed record PresignedUrlResult(string Url, DateTimeOffset ExpiresAt);

/// <summary>Metadata for a stored object.</summary>
/// <param name="ContentType">MIME type of the object.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="LastModified">UTC timestamp of last modification.</param>
public sealed record ObjectMetadataResult(string ContentType, long Size, DateTimeOffset LastModified);
