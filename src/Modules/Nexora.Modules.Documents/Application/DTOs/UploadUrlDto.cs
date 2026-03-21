namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Result of generating a presigned upload URL.</summary>
/// <param name="UploadUrl">Presigned URL for direct file upload.</param>
/// <param name="StorageKey">Object key assigned in storage for tracking.</param>
/// <param name="ExpiresAt">UTC timestamp when the presigned URL expires.</param>
public sealed record UploadUrlDto(string UploadUrl, string StorageKey, DateTimeOffset ExpiresAt);
