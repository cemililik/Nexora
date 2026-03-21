using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Api;

/// <summary>Minimal API endpoints for digital signature operations.</summary>
public static class SignatureEndpoints
{
    /// <summary>Maps signature management endpoints.</summary>
    public static void MapSignatureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/signatures")
            .RequireAuthorization();

        group.MapGet("/", async (
            int? page, int? pageSize, Guid? documentId, string? status,
            ISender sender, CancellationToken ct) =>
        {
            var query = new GetSignatureRequestsQuery(page ?? 1, pageSize ?? 20, documentId, status);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<SignatureRequestDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<SignatureRequestDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSignatureRequestByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<SignatureRequestDetailDto>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<SignatureRequestDetailDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateSignatureRequestCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/documents/signatures/{result.Value!.Id}",
                    ApiEnvelope<SignatureRequestDetailDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<SignatureRequestDetailDto>.Fail(result.Error!));
        });

        group.MapPost("/{id:guid}/send", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SendSignatureRequestCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<object>.Success(new { }, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_documents_error_signature_request_not_found" =>
                    Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        group.MapPost("/{id:guid}/sign", async (Guid id, SignRequest body, ISender sender, HttpContext httpContext, CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var command = new RecordSignatureCommand(id, body.RecipientId, body.SignatureData, ipAddress);
            var result = await sender.Send(command, ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<object>.Success(new { }, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_documents_error_signature_request_not_found" =>
                    Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        group.MapPost("/{id:guid}/decline", async (Guid id, DeclineRequest body, ISender sender, CancellationToken ct) =>
        {
            var command = new DeclineSignatureCommand(id, body.RecipientId);
            var result = await sender.Send(command, ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<object>.Success(new { }, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_documents_error_signature_request_not_found" =>
                    Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CancelSignatureRequestCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope<object>.Success(new { }, result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_documents_error_signature_request_not_found" =>
                    Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });
    }
}

/// <summary>Request body for recording a signature.</summary>
public sealed record SignRequest(Guid RecipientId, string SignatureData);

/// <summary>Request body for declining a signature.</summary>
public sealed record DeclineRequest(Guid RecipientId);
