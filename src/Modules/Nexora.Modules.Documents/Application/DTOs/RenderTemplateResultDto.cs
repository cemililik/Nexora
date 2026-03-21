namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Result of rendering a document from a template.</summary>
/// <param name="DocumentId">Generated document identifier.</param>
/// <param name="Name">Generated document name.</param>
/// <param name="StorageKey">Storage key of the rendered document.</param>
public sealed record RenderTemplateResultDto(
    Guid DocumentId,
    string Name,
    string StorageKey);
