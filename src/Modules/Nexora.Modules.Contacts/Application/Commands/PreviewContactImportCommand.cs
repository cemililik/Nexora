using FluentValidation;
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

/// <summary>
/// Parses the first 5 rows of a previously uploaded import file so the admin UI
/// can render a column-mapping table. This command is stateless — it does NOT
/// create an <c>ImportJob</c> record.
/// </summary>
public sealed record PreviewContactImportCommand(
    string StorageKey,
    string FileFormat) : ICommand<ContactImportPreviewDto>;

/// <summary>Validates preview input.</summary>
public sealed class PreviewContactImportValidator : AbstractValidator<PreviewContactImportCommand>
{
    private static readonly string[] ValidFormats = ["csv", "xlsx"];

    public PreviewContactImportValidator()
    {
        RuleFor(x => x.StorageKey)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_storage_key_required");

        RuleFor(x => x.FileFormat)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_format_required")
            .Must(f => ValidFormats.Contains(f.ToLowerInvariant()))
            .WithMessage("lockey_contacts_validation_import_format_invalid");
    }
}

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
            return Result<ContactImportPreviewDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<ContactImportPreviewDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_organization_context"));

        var expectedPrefix = $"{orgId}/contacts/imports/";
        if (!request.StorageKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Preview rejected: storage key {StorageKey} does not match expected prefix for organization {OrganizationId} in tenant {TenantId}",
                request.StorageKey, orgId, tenantId);
            return Result<ContactImportPreviewDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_invalid_storage_key"));
        }

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        var exists = await fileStorageService.ObjectExistsAsync(
            bucketName, request.StorageKey, cancellationToken);
        if (!exists)
            return Result<ContactImportPreviewDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_file_not_found"));

        var content = await fileStorageService.GetObjectAsync(
            bucketName, request.StorageKey, cancellationToken);

        var headers = ContactImportParser.ParseHeaders(content, request.FileFormat);
        var allRows = ContactImportParser.ParseRows(content, request.FileFormat);
        var preview = allRows.Take(PreviewRowCount).ToList();

        logger.LogInformation(
            "Contact import preview generated for tenant {TenantId}: {HeaderCount} headers, {TotalRows} total rows",
            tenantId, headers.Count, allRows.Count);

        var dto = new ContactImportPreviewDto(headers, preview, allRows.Count);
        return Result<ContactImportPreviewDto>.Success(
            dto, LocalizedMessage.Of("lockey_contacts_import_preview_generated"));
    }
}
