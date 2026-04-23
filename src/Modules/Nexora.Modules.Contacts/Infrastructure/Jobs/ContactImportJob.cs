using System.ComponentModel;
using System.Text.Json;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.Services;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure.Parsers;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Domain.Events;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Infrastructure.Jobs;

/// <summary>Parameters for a contact import job.</summary>
public sealed record ContactImportJobParams : JobParams
{
    public required string FileName { get; init; }
    public required string FileFormat { get; init; }
    public required string StorageKey { get; init; }
    public Guid? OrganizationIdGuid { get; init; }
    public Guid ImportJobId { get; init; }
}

/// <summary>
/// Background job that downloads the import file from MinIO via storage key,
/// parses CSV/Excel content, applies the user-supplied column mapping, and imports
/// contacts in batches. Runs on the <c>bulk</c> Hangfire queue per infrastructure standards.
/// </summary>
[Queue("bulk")]
[DisplayName("contacts:bulk-import")]
public sealed class ContactImportJob(
    ITenantContextAccessor tenantContextAccessor,
    ContactsDbContext dbContext,
    IFileStorageService fileStorageService,
    IContactDuplicateMatcher duplicateMatcher,
    IOutbox outbox,
    IOptions<StorageOptions> storageOptions,
    ILogger<ContactImportJob> logger) : NexoraJob<ContactImportJobParams>(tenantContextAccessor, logger)
{
    private const int BatchSize = 100;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(ContactImportJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);
        var orgId = parameters.OrganizationIdGuid ?? Guid.Empty;
        var importJobId = ImportJobId.From(parameters.ImportJobId);

        var importJob = await dbContext.ImportJobs.FindAsync([importJobId], ct);
        if (importJob is null)
        {
            logger.LogWarning("ImportJob {ImportJobId} not found, aborting", importJobId);
            return;
        }

        // Idempotency guard — only skip if already completed. Processing is left resumable
        // so Hangfire retries after a mid-job crash can pick up where the previous attempt left off.
        if (importJob.Status == ImportJobStatus.Completed)
        {
            logger.LogWarning("ImportJob {ImportJobId} already completed, skipping", importJobId);
            return;
        }

        var columnMapping = DeserializeMapping(importJob.ColumnMappingJson);

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        logger.LogInformation(
            "Starting contact import from {FileName} ({Format}) using storage key {StorageKey}",
            parameters.FileName, parameters.FileFormat, parameters.StorageKey);

        List<IReadOnlyDictionary<string, string?>> rows;

        try
        {
            var fileContent = await fileStorageService.GetObjectAsync(
                bucketName, parameters.StorageKey, ct);

            using var contentStream = new MemoryStream(fileContent);
            rows = ContactImportParser.ParseRows(contentStream, parameters.FileFormat);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Failed to read import file for job {ImportJobId}", importJobId);
            importJob.MarkProcessing(0);
            importJob.MarkFailed(ex.Message);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "Malformed import file for job {ImportJobId}", importJobId);
            importJob.MarkProcessing(0);
            importJob.MarkFailed(ex.Message);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (NotSupportedException ex)
        {
            logger.LogError(ex, "Unsupported import format for job {ImportJobId}", importJobId);
            importJob.MarkProcessing(0);
            importJob.MarkFailed(ex.Message);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        var totalRows = rows.Count;
        var successCount = 0;
        var errorCount = 0;
        var skippedCount = 0;

        if (importJob.Status == ImportJobStatus.Queued)
        {
            importJob.MarkProcessing(totalRows);
            await dbContext.SaveChangesAsync(ct);
        }

        for (var i = 0; i < totalRows; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batch = rows.Skip(i).Take(BatchSize).ToList();

            // Pre-compute mapped rows so the email set can be collected in one pass
            // before hitting the database with a single bulk duplicate probe (avoids N+1).
            var mappedBatch = batch.Select(r => ApplyMapping(r, columnMapping)).ToList();
            var normalizedEmailSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapped in mappedBatch)
            {
                if (!string.IsNullOrWhiteSpace(mapped.Email))
                    normalizedEmailSet.Add(mapped.Email.Trim().ToLowerInvariant());
            }

            var existingByEmail = normalizedEmailSet.Count == 0
                ? new Dictionary<string, Guid>(0)
                : await duplicateMatcher.FindExistingByEmailsAsync(tenantId, orgId, normalizedEmailSet, ct);

            for (var j = 0; j < batch.Count; j++)
            {
                var row = mappedBatch[j];

                try
                {
                    var normalizedEmail = string.IsNullOrWhiteSpace(row.Email)
                        ? null
                        : row.Email.Trim().ToLowerInvariant();

                    if (normalizedEmail is not null
                        && existingByEmail.ContainsKey(normalizedEmail))
                    {
                        logger.LogDebug(
                            "Skipping duplicate contact for ImportJob {ImportJobId} at row {RowIndex}",
                            importJobId, i + j);
                        skippedCount++;
                        continue;
                    }

                    var contactType = Enum.TryParse<ContactType>(row.Type, ignoreCase: true, out var parsedType)
                        ? parsedType
                        : ContactType.Individual;

                    var contact = Contact.Create(
                        tenantId, orgId, contactType,
                        row.FirstName, row.LastName, row.CompanyName,
                        row.Email, row.Phone, ContactSource.Import);

                    await dbContext.Contacts.AddAsync(contact, ct);
                    successCount++;
                }
                catch (DomainException ex)
                {
                    logger.LogWarning(ex, "Domain validation failed for row {RowIndex}", i + j);
                    errorCount++;
                }
                catch (FormatException ex)
                {
                    logger.LogWarning(ex, "Failed to parse row {RowIndex}", i + j);
                    errorCount++;
                }
                catch (ArgumentException ex)
                {
                    logger.LogWarning(ex, "Invalid data in row {RowIndex}", i + j);
                    errorCount++;
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Failed to import row {RowIndex}", i + j);
                    errorCount++;
                }
            }

            await dbContext.SaveChangesAsync(ct);

            var processed = Math.Min(i + BatchSize, totalRows);
            importJob.UpdateProgress(processed, successCount, errorCount, skippedCount);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Import progress: {Processed}/{Total} (success: {Success}, errors: {Errors}, skipped: {Skipped})",
                processed, totalRows, successCount, errorCount, skippedCount);
        }

        // Atomic: mark completed + enqueue outbox event in a single SaveChangesAsync
        // so the job's terminal state and the downstream notification are never out of sync.
        importJob.MarkCompleted();

        await outbox.EnqueueAsync(new ContactImportCompletedIntegrationEvent
        {
            TenantId = parameters.TenantId,
            ImportJobId = parameters.ImportJobId,
            TotalRows = totalRows,
            SuccessCount = successCount,
            ErrorCount = errorCount,
            SkippedCount = skippedCount
        }, ct);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Contact import completed. Total: {Total}, Success: {Success}, Errors: {Errors}, Skipped: {Skipped}",
            totalRows, successCount, errorCount, skippedCount);
    }

    private static IReadOnlyDictionary<string, string>? DeserializeMapping(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return parsed is null
            ? null
            : new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Projects a header-keyed row into a strongly-typed <see cref="ContactImportRow"/>.
    /// When <paramref name="mapping"/> is null, source headers are treated as identity
    /// (i.e. header name == target field name, case-insensitive).
    /// </summary>
    public static ContactImportRow ApplyMapping(
        IReadOnlyDictionary<string, string?> row,
        IReadOnlyDictionary<string, string>? mapping)
    {
        string? Lookup(string target)
        {
            if (mapping is null)
            {
                return row.TryGetValue(target, out var v) ? v : null;
            }

            foreach (var (source, mapped) in mapping)
            {
                if (string.Equals(mapped, target, StringComparison.OrdinalIgnoreCase)
                    && row.TryGetValue(source, out var v))
                {
                    return v;
                }
            }
            return null;
        }

        return new ContactImportRow
        {
            Type = Lookup("Type"),
            FirstName = Lookup("FirstName"),
            LastName = Lookup("LastName"),
            CompanyName = Lookup("CompanyName"),
            Email = Lookup("Email"),
            Phone = Lookup("Phone"),
            Title = Lookup("Title"),
        };
    }
}

/// <summary>Represents a single normalized row from a contact import file.</summary>
public sealed record ContactImportRow
{
    public string? Type { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? CompanyName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Title { get; init; }
}
