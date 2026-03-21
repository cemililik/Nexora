using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Application.Services;

/// <summary>
/// Service responsible for archiving signed documents into a dedicated "Signed Documents" folder.
/// </summary>
public interface IDocumentArchivalService
{
    /// <summary>
    /// Archives a document after signature completion by moving it to a "Signed Documents" system folder.
    /// Creates the folder if it doesn't exist for the given tenant/organization.
    /// </summary>
    Task ArchiveSignedDocumentAsync(
        DocumentId documentId,
        SignatureRequestId signatureRequestId,
        Guid tenantId,
        Guid organizationId,
        CancellationToken ct = default);
}
