using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Infrastructure.Parsers;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Downloads the uploaded file from storage and returns the first 5 rows.</summary>
public sealed class PreviewContactImportHandler(
    IFileStorageService fileStorageService,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<StorageOptions> storageOptions,
    ILogger<PreviewContactImportHandler> logger)
    : ICommandHandler<PreviewContactImportCommand, ContactImportPreviewDto>
{
    private const int PreviewRowCount = 5;

    public async Task<Result<ContactImportPreviewDto>> Handle(
        PreviewContactImportCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
        {
            logger.LogWarning("Contact import preview rejected — invalid tenant context");
            return Result<ContactImportPreviewDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));
        }

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
        {
            logger.LogWarning(
                "Contact import preview rejected — invalid organization context for tenant {TenantId}",
                tenantId);
            return Result<ContactImportPreviewDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_organization_context"));
        }

        var expectedPrefix = $"{orgId}/contacts/imports/";
        if (!request.StorageKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Invalid storage key prefix on import preview for tenant {TenantId} organization {OrganizationId}",
                tenantId, orgId);
            return Result<ContactImportPreviewDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_invalid_storage_key"));
        }

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        var exists = await fileStorageService.ObjectExistsAsync(
            bucketName, request.StorageKey, cancellationToken);
        if (!exists)
        {
            logger.LogWarning(
                "Contact import preview rejected — file not found for tenant {TenantId} organization {OrganizationId}",
                tenantId, orgId);
            return Result<ContactImportPreviewDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_file_not_found"));
        }

        var content = await fileStorageService.GetObjectAsync(
            bucketName, request.StorageKey, cancellationToken);

        // Parser consumes the stream sequentially — open a fresh MemoryStream per call
        // so ParseRows starts at position 0 after ParseHeaders has read the header row.
        using var headerStream = new MemoryStream(content);
        var headers = ContactImportParser.ParseHeaders(headerStream, request.FileFormat);
        using var rowsStream = new MemoryStream(content);
        var allRows = ContactImportParser.ParseRows(rowsStream, request.FileFormat);
        var preview = allRows.Take(PreviewRowCount).ToList();

        logger.LogInformation(
            "Contact import preview generated for tenant {TenantId}: {HeaderCount} headers, {TotalRows} total rows",
            tenantId, headers.Count, allRows.Count);

        var dto = new ContactImportPreviewDto(headers, preview, allRows.Count);
        return Result<ContactImportPreviewDto>.Success(
            dto, LocalizedMessage.Of("lockey_contacts_import_preview_generated"));
    }
}
