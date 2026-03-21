using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.Services;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;

namespace Nexora.Modules.Documents.Infrastructure.Services;

/// <summary>
/// Archives signed documents by moving them to a dedicated "Signed Documents" system folder.
/// </summary>
public sealed class DocumentArchivalService(
    DocumentsDbContext dbContext,
    ILogger<DocumentArchivalService> logger) : IDocumentArchivalService
{
    /// <summary>The name of the system folder for signed documents.</summary>
    public const string SignedDocumentsFolderName = "Signed Documents";

    /// <inheritdoc />
    public async Task ArchiveSignedDocumentAsync(
        DocumentId documentId,
        SignatureRequestId signatureRequestId,
        Guid tenantId,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, ct);

        if (document is null)
        {
            logger.LogWarning(
                "Document {DocumentId} not found for archival after signature completion {SignatureRequestId}",
                documentId.Value, signatureRequestId.Value);
            return;
        }

        var signedFolder = await GetOrCreateSignedDocumentsFolderAsync(tenantId, organizationId, document.UploadedByUserId, ct);

        document.MoveToFolder(signedFolder.Id);
        document.Archive();

        // Single SaveChangesAsync covers both new folder (if created) and document changes
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Document {DocumentId} archived to '{FolderName}' after signature request {SignatureRequestId} completed",
            documentId.Value, SignedDocumentsFolderName, signatureRequestId.Value);
    }

    private async Task<Folder> GetOrCreateSignedDocumentsFolderAsync(
        Guid tenantId, Guid organizationId, Guid ownerUserId, CancellationToken ct)
    {
        var existingFolder = await dbContext.Folders
            .FirstOrDefaultAsync(f =>
                f.TenantId == tenantId &&
                f.OrganizationId == organizationId &&
                f.Name == SignedDocumentsFolderName &&
                f.IsSystem, ct);

        if (existingFolder is not null)
            return existingFolder;

        var folder = Folder.Create(
            tenantId, organizationId, SignedDocumentsFolderName, ownerUserId,
            isSystem: true);

        await dbContext.Folders.AddAsync(folder, ct);

        logger.LogInformation(
            "Created system folder '{FolderName}' for tenant {TenantId} organization {OrganizationId}",
            SignedDocumentsFolderName, tenantId, organizationId);

        return folder;
    }
}
