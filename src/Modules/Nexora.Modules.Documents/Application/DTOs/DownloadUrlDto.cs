namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Result of generating a presigned download URL.</summary>
/// <param name="DownloadUrl">Presigned URL for direct file download.</param>
/// <param name="ExpiresAt">UTC timestamp when the presigned URL expires.</param>
public sealed record DownloadUrlDto(string DownloadUrl, DateTimeOffset ExpiresAt);
