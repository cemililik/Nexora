using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Host.Endpoints;

/// <summary>
/// Admin-panel endpoints for organization-scope compliance configuration (ADR-0025).
/// Exposes the three-tier resolver (platform cap → tenant default → org override) so
/// organization admins with <c>contacts.gdpr.settings_manage</c> can view and toggle
/// compliance knobs such as GDPR hard-delete.
/// </summary>
public static class ComplianceConfigEndpoints
{
    /// <summary>Maps compliance config endpoints under /api/v1/settings/compliance.</summary>
    public static void MapComplianceConfigEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/settings/compliance")
            .RequireAuthorization("contacts.gdpr.settings_manage");

        group.MapGet("/", GetAllKeysAsync);
        group.MapGet("/{key}", GetKeyAsync);
        group.MapPut("/{key}", SetKeyAsync);
        group.MapDelete("/{key}", ClearKeyAsync);
    }

    /// <summary>
    /// Keys surfaced to the admin UI. Kept in one place so the frontend does not have
    /// to guess which keys exist — future keys (retention windows, residency, etc.) get
    /// added here and rendered automatically.
    /// </summary>
    private static readonly string[] ManagedKeys =
    {
        ComplianceKey.GdprHardDeleteEnabled
    };

    private static async Task<IResult> GetAllKeysAsync(
        IConfigurationResolver resolver,
        CancellationToken ct)
    {
        var items = new List<ComplianceKeySummaryDto>(ManagedKeys.Length);
        foreach (var key in ManagedKeys)
        {
            var resolved = await resolver.GetResolvedAsync<bool>(key, ct);
            items.Add(ToSummary(key, resolved));
        }

        return Results.Ok(ApiEnvelope<IReadOnlyList<ComplianceKeySummaryDto>>.Success(items));
    }

    private static async Task<IResult> GetKeyAsync(
        string key,
        IConfigurationResolver resolver,
        CancellationToken ct)
    {
        if (!IsManagedKey(key))
            return NotFound(key);

        var resolved = await resolver.GetResolvedAsync<bool>(key, ct);
        return Results.Ok(ApiEnvelope<ComplianceKeySummaryDto>.Success(ToSummary(key, resolved)));
    }

    private static async Task<IResult> SetKeyAsync(
        string key,
        SetComplianceOverrideRequest request,
        IConfigurationResolver resolver,
        ILogger<ComplianceConfigEndpointsMarker> logger,
        CancellationToken ct)
    {
        if (!IsManagedKey(key))
            return NotFound(key);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(ApiEnvelope<object>.Fail(new Error(
                LocalizedMessage.Of("lockey_validation_required",
                    new Dictionary<string, string> { ["field"] = "Reason" }))));
        }
        if (request.Reason.Length > 500)
        {
            return Results.BadRequest(ApiEnvelope<object>.Fail(new Error(
                LocalizedMessage.Of("lockey_validation_max_length",
                    new Dictionary<string, string> { ["field"] = "Reason", ["max"] = "500" }))));
        }

        try
        {
            await resolver.SetOrgOverrideAsync(key, request.Value, request.Reason, ct);
        }
        catch (ComplianceCapViolationException ex)
        {
            logger.LogWarning(ex,
                "Compliance override rejected by platform cap for key {Key}", key);
            return Results.Conflict(ApiEnvelope<object>.Fail(new Error(
                LocalizedMessage.Of(ex.LocalizationKey,
                    new Dictionary<string, string> { ["key"] = key }))));
        }

        var resolved = await resolver.GetResolvedAsync<bool>(key, ct);
        return Results.Ok(ApiEnvelope<ComplianceKeySummaryDto>.Success(
            ToSummary(key, resolved),
            LocalizedMessage.Of("lockey_settings_compliance_override_saved")));
    }

    private static async Task<IResult> ClearKeyAsync(
        string key,
        string reason,
        IConfigurationResolver resolver,
        CancellationToken ct)
    {
        if (!IsManagedKey(key))
            return NotFound(key);

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Results.BadRequest(ApiEnvelope<object>.Fail(new Error(
                LocalizedMessage.Of("lockey_validation_required",
                    new Dictionary<string, string> { ["field"] = "Reason" }))));
        }

        await resolver.ClearOrgOverrideAsync(key, reason, ct);

        var resolved = await resolver.GetResolvedAsync<bool>(key, ct);
        return Results.Ok(ApiEnvelope<ComplianceKeySummaryDto>.Success(
            ToSummary(key, resolved),
            LocalizedMessage.Of("lockey_settings_compliance_override_cleared")));
    }

    private static bool IsManagedKey(string key)
        => Array.IndexOf(ManagedKeys, key) >= 0;

    private static IResult NotFound(string key) =>
        Results.NotFound(ApiEnvelope<object>.Fail(new Error(
            LocalizedMessage.Of("lockey_error_compliance_key_not_managed",
                new Dictionary<string, string> { ["key"] = key }))));

    private static ComplianceKeySummaryDto ToSummary(string key, ResolvedConfiguration<bool> resolved) =>
        new(
            Key: key,
            EffectiveValue: resolved.Effective,
            TenantDefault: resolved.TenantDefault,
            OrgOverride: resolved.OrgOverride,
            WinningLayer: resolved.WinningLayer.ToString(),
            CapAllowed: resolved.Cap.Allowed,
            CapForced: resolved.Cap.Forced);
}

/// <summary>Marker type used solely to resolve a typed <c>ILogger</c>.</summary>
internal sealed class ComplianceConfigEndpointsMarker
{
}

/// <summary>Request body for PUT /settings/compliance/{key}.</summary>
public sealed record SetComplianceOverrideRequest(bool Value, string Reason);

/// <summary>
/// Response row for compliance config endpoints. Shows each layer's contribution so the
/// admin UI can render "tenant default / org override / effective / cap badge" without
/// extra round-trips.
/// </summary>
public sealed record ComplianceKeySummaryDto(
    string Key,
    bool EffectiveValue,
    bool? TenantDefault,
    bool? OrgOverride,
    string WinningLayer,
    bool CapAllowed,
    bool CapForced);
