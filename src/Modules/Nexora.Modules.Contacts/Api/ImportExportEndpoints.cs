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

        group.MapPost("/import", async (ConfirmImportRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new StartContactImportCommand(
                request.FileName, request.FileFormat, request.StorageKey);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Accepted(
                    $"/api/v1/contacts/contacts/import/{result.Value!.JobId}",
                    ApiEnvelope<ImportJobDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<ImportJobDto>.Fail(result.Error!));
        });

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
        });

        group.MapPost("/export", async (StartExportRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new StartContactExportCommand(
                request.Format, request.StatusFilter, request.TypeFilter);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Accepted(
                    $"/api/v1/contacts/contacts/export",
                    ApiEnvelope<ExportJobDto>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<ExportJobDto>.Fail(result.Error!));
        });
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
    string StorageKey);

/// <summary>Request body for starting a contact export.</summary>
public sealed record StartExportRequest(
    string Format,
    string? StatusFilter = null,
    string? TypeFilter = null);
