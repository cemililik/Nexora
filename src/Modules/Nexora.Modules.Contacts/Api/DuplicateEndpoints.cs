using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for duplicate detection and contact merge.</summary>
public static class DuplicateEndpoints
{
    /// <summary>Maps duplicate detection and merge endpoints.</summary>
    public static void MapDuplicateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/contacts")
            .RequireAuthorization();

        group.MapGet("/{contactId:guid}/duplicates", async (Guid contactId, int? threshold, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDuplicateContactsQuery(contactId, threshold ?? 40), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<DuplicateContactDto>>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<DuplicateContactDto>>.Fail(result.Error!));
        });

        group.MapPost("/merge", async (MergeContactsRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new MergeContactsCommand(
                request.PrimaryContactId, request.SecondaryContactId,
                request.UseSecondaryEmail, request.UseSecondaryPhone);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<MergeResultDto>.Success(result.Value!, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_primary_contact_not_found" =>
                        Results.NotFound(ApiEnvelope<MergeResultDto>.Fail(result.Error)),
                    "lockey_contacts_error_secondary_contact_not_found" =>
                        Results.NotFound(ApiEnvelope<MergeResultDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<MergeResultDto>.Fail(result.Error))
                };
        });
    }
}

/// <summary>Request body for merging contacts.</summary>
public sealed record MergeContactsRequest(
    Guid PrimaryContactId,
    Guid SecondaryContactId,
    bool UseSecondaryEmail = false,
    bool UseSecondaryPhone = false);
