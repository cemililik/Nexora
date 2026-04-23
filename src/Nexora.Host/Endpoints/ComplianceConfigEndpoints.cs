using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            // Resolve with bool? so the summary can distinguish "unset" (null) from
            // "set to false" — the admin UI renders these differently ("Not set" vs
            // "Disabled").
            var resolved = await resolver.GetResolvedAsync<bool?>(key, ct);
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

        var resolved = await resolver.GetResolvedAsync<bool?>(key, ct);
        return Results.Ok(ApiEnvelope<ComplianceKeySummaryDto>.Success(ToSummary(key, resolved)));
    }

    private static async Task<IResult> SetKeyAsync(
        string key,
        SetComplianceOverrideRequest request,
        IConfigurationResolver resolver,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!IsManagedKey(key))
            return NotFound(key);

        var logger = loggerFactory.CreateLogger("Nexora.Host.Endpoints.ComplianceConfig");

        // Minimal APIs don't auto-invoke FluentValidation; Set and Clear share the same
        // reason-shape rules so they go through ValidateReason below.
        if (ValidateReason(request.Reason) is { } problem) return problem;

        try
        {
            await resolver.SetOrgOverrideAsync(key, request.Value, request.Reason, ct);
        }
        catch (ComplianceCapViolationException ex)
        {
            logger.LogWarning(ex,
                "Compliance override rejected for key {Key} (forced={IsForced}, allowed={Allowed})",
                ex.Key, ex.IsForced, ex.Allowed);
            var meta = new Dictionary<string, string> { ["key"] = key };
            if (ex.IsForced && ex.ForcedValue is not null)
                meta["forcedValue"] = ex.ForcedValue;
            return Results.Conflict(ApiEnvelope<object>.Fail(new Error(
                LocalizedMessage.Of(ex.LocalizationKey, meta))));
        }

        var resolved = await resolver.GetResolvedAsync<bool?>(key, ct);
        return Results.Ok(ApiEnvelope<ComplianceKeySummaryDto>.Success(
            ToSummary(key, resolved),
            LocalizedMessage.Of("lockey_identity_compliance_override_saved")));
    }

    private static async Task<IResult> ClearKeyAsync(
        string key,
        [FromBody] ClearComplianceOverrideRequest request,
        IConfigurationResolver resolver,
        CancellationToken ct)
    {
        if (!IsManagedKey(key))
            return NotFound(key);

        if (ValidateReason(request.Reason) is { } problem) return problem;

        await resolver.ClearOrgOverrideAsync(key, request.Reason, ct);

        var resolved = await resolver.GetResolvedAsync<bool?>(key, ct);
        return Results.Ok(ApiEnvelope<ComplianceKeySummaryDto>.Success(
            ToSummary(key, resolved),
            LocalizedMessage.Of("lockey_identity_compliance_override_cleared")));
    }

    private static bool IsManagedKey(string key)
        => Array.IndexOf(ManagedKeys, key) >= 0;

    /// <summary>
    /// Shared request-shape validation for the "reason" string used by both Set and
    /// Clear endpoints. Returns <see langword="null"/> when the reason is valid, or a
    /// <c>Results.BadRequest(ApiEnvelope...)</c> wrapping the localized error otherwise.
    /// </summary>
    private static IResult? ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Results.BadRequest(ApiEnvelope<object>.Fail(new Error(
                LocalizedMessage.Of("lockey_validation_required",
                    new Dictionary<string, string> { ["field"] = "Reason" }))));
        }
        if (reason.Length > 500)
        {
            return Results.BadRequest(ApiEnvelope<object>.Fail(new Error(
                LocalizedMessage.Of("lockey_validation_max_length",
                    new Dictionary<string, string> { ["field"] = "Reason", ["max"] = "500" }))));
        }
        return null;
    }

    private static IResult NotFound(string key) =>
        Results.NotFound(ApiEnvelope<object>.Fail(new Error(
            LocalizedMessage.Of("lockey_identity_error_compliance_key_not_managed",
                new Dictionary<string, string> { ["key"] = key }))));

    private static ComplianceKeySummaryDto ToSummary(string key, ResolvedConfiguration<bool?> resolved)
    {
        // `Effective` is bool? — may be null when no layer supplies a value; surface as
        // `false` to the UI contract but `TenantDefault`/`OrgOverride` stay nullable so
        // the UI can distinguish "not set" from "set to false".
        var effective = resolved.Effective ?? false;
        return new ComplianceKeySummaryDto(
            Key: key,
            EffectiveValue: effective,
            TenantDefault: resolved.TenantDefault,
            OrgOverride: resolved.OrgOverride,
            WinningLayer: resolved.WinningLayer.ToString(),
            CapAllowed: resolved.Cap.Allowed,
            CapForced: resolved.Cap.Forced);
    }
}

/// <summary>Request body for PUT /settings/compliance/{key}.</summary>
public sealed record SetComplianceOverrideRequest(bool Value, string Reason);

/// <summary>
/// Request body for DELETE /settings/compliance/{key}. The reason travels in the body
/// rather than a query string so free-text justification (potentially PII) does not
/// leak into access logs, browser history, or referrer headers.
/// </summary>
public sealed record ClearComplianceOverrideRequest(string Reason);

/// <summary>
/// Response row for compliance config endpoints. Shows each layer's contribution so the
/// admin UI can render "tenant default / org override / effective / cap badge" without
/// extra round-trips. `TenantDefault` / `OrgOverride` are nullable so "not set" renders
/// distinctly from "set to false".
/// </summary>
public sealed record ComplianceKeySummaryDto(
    string Key,
    bool EffectiveValue,
    bool? TenantDefault,
    bool? OrgOverride,
    string WinningLayer,
    bool CapAllowed,
    bool CapForced);
