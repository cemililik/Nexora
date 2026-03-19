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

        group.MapPost("/import", async (StartImportRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new StartContactImportCommand(
                request.FileName, request.FileFormat, request.FileContent);
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

/// <summary>Request body for starting a contact import.</summary>
public sealed record StartImportRequest(
    string FileName,
    string FileFormat,
    byte[] FileContent);

/// <summary>Request body for starting a contact export.</summary>
public sealed record StartExportRequest(
    string Format,
    string? StatusFilter = null,
    string? TypeFilter = null);
