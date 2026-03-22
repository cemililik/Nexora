using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;

namespace Nexora.Modules.Contacts.Infrastructure.Jobs;

/// <summary>Parameters for a contact import job.</summary>
public sealed record ContactImportJobParams : JobParams
{
    public required string FileName { get; init; }
    public required string FileFormat { get; init; }
    public required string StorageKey { get; init; }
    public Guid? OrganizationIdGuid { get; init; }
}

/// <summary>
/// Background job that downloads the import file from MinIO via storage key,
/// parses CSV/Excel content, and imports contacts in batches.
/// Performs duplicate detection and reports progress.
/// </summary>
public sealed class ContactImportJob(
    ITenantContextAccessor tenantContextAccessor,
    ContactsDbContext dbContext,
    IFileStorageService fileStorageService,
    IOptions<StorageOptions> storageOptions,
    ILogger<ContactImportJob> logger) : NexoraJob<ContactImportJobParams>(tenantContextAccessor, logger)
{
    private const int BatchSize = 100;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(ContactImportJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);
        var orgId = parameters.OrganizationIdGuid ?? Guid.Empty;

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        logger.LogInformation(
            "Starting contact import from {FileName} ({Format}) using storage key {StorageKey}",
            parameters.FileName, parameters.FileFormat, parameters.StorageKey);

        // Download file content from MinIO using storage key
        var fileContent = await fileStorageService.GetObjectAsync(
            bucketName, parameters.StorageKey, ct);

        var rows = ParseFile(fileContent, parameters.FileFormat);
        var totalRows = rows.Count;
        var successCount = 0;
        var errorCount = 0;

        for (var i = 0; i < totalRows; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batch = rows.Skip(i).Take(BatchSize).ToList();

            foreach (var row in batch)
            {
                try
                {
                    var existingContact = !string.IsNullOrWhiteSpace(row.Email)
                        ? await dbContext.Contacts.FirstOrDefaultAsync(
                            c => c.TenantId == tenantId && c.Email == row.Email, ct)
                        : null;

                    if (existingContact is not null)
                    {
                        logger.LogDebug("Skipping duplicate contact with email {Email}", row.Email);
                        errorCount++;
                        continue;
                    }

                    var contact = Contact.Create(
                        tenantId, orgId, ContactType.Individual,
                        row.FirstName, row.LastName, row.CompanyName,
                        row.Email, row.Phone, ContactSource.Import);

                    await dbContext.Contacts.AddAsync(contact, ct);
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to import row {RowIndex}", i + batch.IndexOf(row));
                    errorCount++;
                }
            }

            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Import progress: {Processed}/{Total} (success: {Success}, errors: {Errors})",
                Math.Min(i + BatchSize, totalRows), totalRows, successCount, errorCount);
        }

        logger.LogInformation(
            "Contact import completed. Total: {Total}, Success: {Success}, Errors: {Errors}",
            totalRows, successCount, errorCount);
    }

    private static List<ImportRow> ParseFile(byte[] content, string format)
    {
        // In production, use CsvHelper for CSV or EPPlus/ClosedXML for XLSX.
        // This is a placeholder that returns an empty list.
        // Real implementation would parse headers and map columns to ImportRow fields.
        _ = content;
        _ = format;
        return [];
    }

    private sealed record ImportRow(
        string FirstName,
        string LastName,
        string? CompanyName,
        string? Email,
        string? Phone);
}
