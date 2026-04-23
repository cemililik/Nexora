using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for contact import and export operations.</summary>
public static class ImportExportEndpoints
{
    /// <summary>Maps import/export endpoints.</summary>
    public static void MapImportExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts")
            .RequireAuthorization();

        group.MapPost("/import/upload-url", async (GenerateImportUploadUrlRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new GenerateImportUploadUrlCommand(
                request.FileName, request.ContentType, request.FileSize);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ImportUploadUrlDto>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<ImportUploadUrlDto>.Fail(result.Error!));
        });

        group.MapPost("/import/preview", async (PreviewImportRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new PreviewContactImportCommand(request.StorageKey, request.FileFormat);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ContactImportPreviewDto>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<ContactImportPreviewDto>.Fail(result.Error!));
        }).RequireAuthorization("contacts.import.execute");

        group.MapPost("/import/validate", async (ValidateImportRequest request, ISender sender, CancellationToken ct) =>
        {
            var mapping = request.ColumnMapping ?? new Dictionary<string, string>();
            var command = new ValidateContactImportCommand(request.StorageKey, request.FileFormat, mapping);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ContactImportValidationDto>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<ContactImportValidationDto>.Fail(result.Error!));
        }).RequireAuthorization("contacts.import.execute");

        group.MapPost("/import", async (ConfirmImportRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new StartContactImportCommand(
                request.FileName, request.FileFormat, request.StorageKey, request.ColumnMapping);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Accepted(
                    $"/api/v1/contacts/contacts/import/{result.Value!.JobId}",
                    ApiEnvelope<ImportJobDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<ImportJobDto>.Fail(result.Error!));
        }).RequireAuthorization("contacts.import.execute");

        group.MapGet("/import/{jobId:guid}", async (Guid jobId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetImportJobStatusQuery(jobId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ImportJobDto>.Success(result.Value!))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_import_job_not_found" =>
                        Results.NotFound(ApiEnvelope<ImportJobDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<ImportJobDto>.Fail(result.Error))
                };
        }).RequireAuthorization("contacts.contact.read");

        group.MapPost("/export", async (StartExportRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new StartContactExportCommand(
                request.Format,
                request.Fields,
                request.CustomFieldIds,
                request.StatusFilter,
                request.TypeFilter,
                request.DateFrom,
                request.DateTo,
                request.DateField);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Accepted(
                    $"/api/v1/contacts/contacts/export/{result.Value!.JobId}",
                    ApiEnvelope<ExportJobDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<ExportJobDto>.Fail(result.Error!));
        }).RequireAuthorization("contacts.export.execute");

        group.MapGet("/export/{jobId:guid}", async (Guid jobId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetExportJobStatusQuery(jobId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ExportJobDto>.Success(result.Value!))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_export_job_not_found" =>
                        Results.NotFound(ApiEnvelope<ExportJobDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<ExportJobDto>.Fail(result.Error))
                };
        }).RequireAuthorization("contacts.contact.read");
    }
}

/// <summary>Request body for generating an import file upload URL.</summary>
public sealed record GenerateImportUploadUrlRequest(
    string FileName,
    string ContentType,
    long FileSize);

/// <summary>Request body for confirming an import after file upload.</summary>
public sealed record ConfirmImportRequest(
    string FileName,
    string FileFormat,
    string StorageKey,
    IReadOnlyDictionary<string, string>? ColumnMapping = null);

/// <summary>Request body for previewing an uploaded import file.</summary>
public sealed record PreviewImportRequest(
    string StorageKey,
    string FileFormat);

/// <summary>Request body for pre-flight validating a mapped import.</summary>
public sealed record ValidateImportRequest(
    string StorageKey,
    string FileFormat,
    IReadOnlyDictionary<string, string>? ColumnMapping);

/// <summary>Request body for starting a contact export.</summary>
public sealed record StartExportRequest(
    string Format,
    IReadOnlyList<string>? Fields = null,
    IReadOnlyList<Guid>? CustomFieldIds = null,
    string? StatusFilter = null,
    string? TypeFilter = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    string? DateField = null);
