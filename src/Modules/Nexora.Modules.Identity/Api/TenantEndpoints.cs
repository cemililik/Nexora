using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Api;

/// <summary>
/// Minimal API endpoints for tenant management.
/// </summary>
public static class TenantEndpoints
{
    /// <summary>Maps tenant CRUD and status endpoints.</summary>
    public static void MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants")
            .RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var query = new GetTenantsQuery(page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<PagedResult<TenantDto>>.Success(result.Value!, result.Message))
                : Results.BadRequest(ApiEnvelope<PagedResult<TenantDto>>.Fail(result.Error!));
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var query = new GetTenantByIdQuery(id);
            var result = await sender.Send(query, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope<TenantDetailDto>.Success(result.Value!, result.Message))
                : Results.NotFound(ApiEnvelope<TenantDetailDto>.Fail(result.Error!));
        });

        group.MapPost("/", async (CreateTenantCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/identity/tenants/{result.Value!.Id}",
                    ApiEnvelope<TenantDto>.Success(result.Value, result.Message))
                : Results.BadRequest(ApiEnvelope<TenantDto>.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}/status", async (Guid id, UpdateTenantStatusRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateTenantStatusCommand(id, request.Action);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope.Fail(result.Error!));
        });

        group.MapPut("/{id:guid}/settings", async (Guid id, UpdateTenantSettingsRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateTenantSettingsCommand(
                id,
                request.DefaultLocale,
                request.DefaultCurrency,
                request.DefaultTimezone,
                request.DefaultDocumentLanguage);
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(ApiEnvelope.Success(result.Message))
                : Results.BadRequest(ApiEnvelope.Fail(result.Error!));
        })
        .RequireAuthorization("identity.tenants.manage")
        .WithSummary("Update tenant locale settings")
        .WithDescription("Updates the default locale, currency, timezone, and document language for a tenant.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // T-008: POST /tenants/demo — platform-operator-only endpoint that
        // pushes demo content into an existing tenant schema. Mirrors the
        // nexora demo:load CLI verb (T-006). Synchronous for now — demo
        // seeding is a short operation; if future scenarios grow large, a
        // Hangfire-backed async mode + polling endpoint can land under the
        // same URL with an `async=true` flag. Permission is Platform-scope
        // (platform.tenants.create_demo), so tenant admins cannot invoke
        // this against their own tenant — explicit by design per T-008 AC.
        group.MapPost("/demo", async (
            CreateDemoEnvironmentRequest request,
            IDemoDataSeeder seeder,
            CancellationToken ct) =>
        {
            if (request.TenantId == Guid.Empty)
            {
                return Results.BadRequest(ApiEnvelope<DemoSeedRunResult>.Fail(
                    new Error(LocalizedMessage.Of("lockey_identity_tenants_demo_invalid_tenant"))));
            }
            if (string.IsNullOrWhiteSpace(request.Scenario))
            {
                return Results.BadRequest(ApiEnvelope<DemoSeedRunResult>.Fail(
                    new Error(LocalizedMessage.Of("lockey_identity_tenants_demo_missing_scenario"))));
            }

            var result = await seeder.SeedAsync(request.TenantId.ToString(), request.Scenario, ct);
            var anyFailed = result.Modules.Any(m => m.Status == DemoSeedStatus.Failed);
            // Partial failures are surfaced via the message-key swap, NOT via
            // ApiEnvelope.Fail: clients need the full per-module outcome
            // array to render the seeded / already-seeded / failed rows, and
            // ApiEnvelope.Fail cannot carry a data payload. The frontend hook
            // (useCreateDemoEnvironment) inspects `modules[].status` to pick
            // between toast.success and toast.warning regardless of the
            // envelope shape — the message-key tells it which one to use.
            return anyFailed
                ? Results.Ok(ApiEnvelope<DemoSeedRunResult>.Success(result,
                    LocalizedMessage.Of("lockey_identity_tenants_demo_completed_with_failures")))
                : Results.Ok(ApiEnvelope<DemoSeedRunResult>.Success(result,
                    LocalizedMessage.Of("lockey_identity_tenants_demo_completed")));
        })
        .RequireAuthorization("platform.tenants.create_demo")
        .WithSummary("Push demo data into an existing tenant (platform operators only)")
        .WithDescription(
            "Invokes IDemoDataSeeder for the given tenant + scenario. Tenant schema must " +
            "already exist; provisioning is a separate admin API. Per-module outcome is " +
            "returned so the UI can show seeded / already-seeded / no-op / failed for each.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

/// <summary>Request body for <c>POST /tenants/demo</c> (T-008).</summary>
public sealed record CreateDemoEnvironmentRequest(Guid TenantId, string Scenario);

public sealed record UpdateTenantStatusRequest(string Action);

/// <summary>Request body for updating tenant locale settings.</summary>
public sealed record UpdateTenantSettingsRequest(
    string DefaultLocale,
    string DefaultCurrency,
    string DefaultTimezone,
    string DefaultDocumentLanguage);
