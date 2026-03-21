namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Cross-module interface for document operations.
/// Implemented by the Documents module, consumed by other modules (CRM, Donations, etc.).
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Generates a document from a template with variable substitution.
    /// Returns the generated document's ID, name, and storage key.
    /// </summary>
    Task<GenerateFromTemplateResult?> GenerateFromTemplateAsync(
        GenerateFromTemplateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets documents linked to a specific entity (e.g., a Contact, Order, etc.).
    /// </summary>
    Task<IReadOnlyList<DocumentSummary>> GetDocumentsByEntityAsync(
        Guid entityId, string entityType, CancellationToken ct = default);
}

/// <summary>Request to generate a document from a template.</summary>
public sealed record GenerateFromTemplateRequest(
    Guid TemplateId,
    Guid FolderId,
    string OutputName,
    Dictionary<string, string> Variables);

/// <summary>Result of document generation from a template.</summary>
public sealed record GenerateFromTemplateResult(
    Guid DocumentId,
    string Name,
    string StorageKey);

/// <summary>Lightweight document data exposed to other modules via SharedKernel.</summary>
public sealed record DocumentSummary(
    Guid Id,
    string Name,
    string MimeType,
    long FileSize,
    string Status,
    Guid? LinkedEntityId,
    string? LinkedEntityType,
    DateTimeOffset CreatedAt);
