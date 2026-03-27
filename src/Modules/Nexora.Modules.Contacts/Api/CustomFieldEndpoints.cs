using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Application.Queries;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Api;

/// <summary>Minimal API endpoints for custom field management.</summary>
public static class CustomFieldEndpoints
{
    /// <summary>Maps custom field endpoints.</summary>
    public static void MapCustomFieldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var definitionGroup = endpoints.MapGroup("/contacts/custom-fields")
            .RequireAuthorization();

        definitionGroup.MapGet("/", async (bool? isActive, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCustomFieldDefinitionsQuery(isActive), ct);
            return Results.Ok(ApiEnvelope<IReadOnlyList<CustomFieldDefinitionDto>>.Success(result.Value!));
        });

        definitionGroup.MapPost("/", async (CreateCustomFieldRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new CreateCustomFieldDefinitionCommand(
                request.FieldName, request.FieldType, request.Options,
                request.IsRequired, request.DisplayOrder);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/contacts/contacts/custom-fields/{result.Value!.Id}",
                    ApiEnvelope<CustomFieldDefinitionDto>.Success(result.Value, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_custom_field_name_duplicate" =>
                        Results.Conflict(ApiEnvelope<CustomFieldDefinitionDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<CustomFieldDefinitionDto>.Fail(result.Error))
                };
        });

        definitionGroup.MapPut("/{id:guid}", async (Guid id, UpdateCustomFieldRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateCustomFieldDefinitionCommand(
                id, request.FieldName, request.Options, request.IsRequired, request.DisplayOrder);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<CustomFieldDefinitionDto>.Success(result.Value!, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_custom_field_definition_not_found" =>
                        Results.NotFound(ApiEnvelope<CustomFieldDefinitionDto>.Fail(result.Error)),
                    "lockey_contacts_error_custom_field_name_duplicate" =>
                        Results.Conflict(ApiEnvelope<CustomFieldDefinitionDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<CustomFieldDefinitionDto>.Fail(result.Error))
                };
        });

        definitionGroup.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteCustomFieldDefinitionCommand(id), ct);
            if (result.IsSuccess)
                return Results.Ok(ApiEnvelope.Success(result.Message));

            return result.Error!.Message.Key switch
            {
                "lockey_contacts_error_custom_field_definition_not_found" =>
                    Results.NotFound(ApiEnvelope<object>.Fail(result.Error)),
                "lockey_contacts_error_custom_field_definition_already_deactivated" =>
                    Results.Conflict(ApiEnvelope<object>.Fail(result.Error)),
                _ => Results.BadRequest(ApiEnvelope<object>.Fail(result.Error))
            };
        });

        // Contact custom field values
        var valueGroup = endpoints.MapGroup("/contacts/{contactId:guid}/custom-fields")
            .RequireAuthorization();

        valueGroup.MapGet("/", async (Guid contactId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetContactCustomFieldsQuery(contactId), ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<IReadOnlyList<ContactCustomFieldDto>>.Success(result.Value!))
                : Results.NotFound(ApiEnvelope<IReadOnlyList<ContactCustomFieldDto>>.Fail(result.Error!));
        });

        valueGroup.MapPut("/{definitionId:guid}", async (Guid contactId, Guid definitionId, SetCustomFieldValueRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new SetContactCustomFieldCommand(contactId, definitionId, request.Value);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<ContactCustomFieldDto>.Success(result.Value!, result.Message))
                : result.Error!.Message.Key switch
                {
                    "lockey_contacts_error_contact_not_found" =>
                        Results.NotFound(ApiEnvelope<ContactCustomFieldDto>.Fail(result.Error)),
                    "lockey_contacts_error_custom_field_definition_not_found" =>
                        Results.NotFound(ApiEnvelope<ContactCustomFieldDto>.Fail(result.Error)),
                    "lockey_contacts_error_custom_field_value_required" =>
                        Results.BadRequest(ApiEnvelope<ContactCustomFieldDto>.Fail(result.Error)),
                    _ => Results.BadRequest(ApiEnvelope<ContactCustomFieldDto>.Fail(result.Error))
                };
        });
    }
}

/// <summary>Request body for creating a custom field definition.</summary>
public sealed record CreateCustomFieldRequest(
    string FieldName,
    string FieldType,
    string? Options = null,
    bool IsRequired = false,
    int DisplayOrder = 0);

/// <summary>Request body for updating a custom field definition.</summary>
public sealed record UpdateCustomFieldRequest(
    string FieldName,
    string? Options,
    bool IsRequired,
    int DisplayOrder);

/// <summary>Request body for setting a custom field value.</summary>
public sealed record SetCustomFieldValueRequest(string? Value);
