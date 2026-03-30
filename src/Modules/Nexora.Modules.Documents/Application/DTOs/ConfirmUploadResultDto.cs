namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Data transfer object for confirm upload response, including version detection flag.</summary>
public sealed record ConfirmUploadResultDto(DocumentDto Document, bool IsVersionUpdate);
