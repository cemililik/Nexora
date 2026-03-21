using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.Services;
using Nexora.Modules.Documents.Domain.Events;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles SignatureCompletedEvent by archiving the signed document
/// into a dedicated "Signed Documents" system folder.
/// </summary>
public sealed class SignatureCompletedArchivalHandler(
    IDocumentArchivalService archivalService,
    DocumentsDbContext dbContext,
    ILogger<SignatureCompletedArchivalHandler> logger) : INotificationHandler<SignatureCompletedEvent>
{
    /// <summary>
    /// Archives the document associated with the completed signature request.
    /// Reads tenant and organization context from the signature request entity (same transaction scope).
    /// </summary>
    public async Task Handle(SignatureCompletedEvent notification, CancellationToken cancellationToken)
    {
        var signatureRequest = await dbContext.SignatureRequests
            .FirstOrDefaultAsync(s => s.Id == notification.RequestId, cancellationToken);

        if (signatureRequest is null)
        {
            logger.LogWarning(
                "SignatureRequest {RequestId} not found for archival on SignatureCompletedEvent",
                notification.RequestId.Value);
            return;
        }

        await archivalService.ArchiveSignedDocumentAsync(
            notification.DocumentId,
            notification.RequestId,
            signatureRequest.TenantId,
            signatureRequest.OrganizationId,
            cancellationToken);
    }
}
