namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO containing a presigned upload URL for contact import files.</summary>
public sealed record ImportUploadUrlDto(string UploadUrl, string StorageKey, DateTimeOffset ExpiresAt);
