using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Nexora.SharedKernel.Abstractions.FeatureFlags;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Api;

/// <summary>
/// T-016: minimal admin endpoints for tenant-scoped feature flags. Read
/// + write only — flag definitions (display name, description, owners)
/// are registry-driven in a future PR; this surface is sufficient for
/// the Milestone C admin-UI MVP.
/// </summary>
public static class FeatureFlagEndpoints
{
    /// <summary>Maps the feature-flag admin endpoints under <c>/feature-flags</c>.</summary>
    public static void MapFeatureFlagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/feature-flags")
            .RequireAuthorization("identity.feature_flags.manage");

        // GET /feature-flags — list every flag set on the current tenant.
        group.MapGet("/", async (
            IFeatureFlagService flags,
            CancellationToken ct) =>
        {
            var all = await flags.GetAllAsync(ct);
            return Results.Ok(ApiEnvelope<IReadOnlyDictionary<string, bool>>.Success(all));
        });

        // GET /feature-flags/{key} — read a single flag's effective value.
        group.MapGet("/{flagKey}", async (
            string flagKey,
            IFeatureFlagService flags,
            CancellationToken ct) =>
        {
            var enabled = await flags.IsEnabledAsync(flagKey, userId: null, ct);
            return Results.Ok(ApiEnvelope<FeatureFlagDto>.Success(new FeatureFlagDto(flagKey, enabled)));
        });

        // PUT /feature-flags/{key} — set a single flag for the current tenant.
        group.MapPut("/{flagKey}", async (
            string flagKey,
            [FromBody] SetFeatureFlagRequest body,
            IFeatureFlagService flags,
            CancellationToken ct) =>
        {
            await flags.SetAsync(flagKey, body.Enabled, ct);
            return Results.Ok(ApiEnvelope<FeatureFlagDto>.Success(new FeatureFlagDto(flagKey, body.Enabled)));
        });
    }
}

/// <summary>Wire shape of a single flag for admin listing.</summary>
public sealed record FeatureFlagDto(string Key, bool Enabled);

/// <summary>Body of <c>PUT /feature-flags/{key}</c>.</summary>
public sealed record SetFeatureFlagRequest(bool Enabled);
