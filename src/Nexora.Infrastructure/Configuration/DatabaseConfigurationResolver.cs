using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Configuration;

/// <summary>
/// Three-tier configuration resolver per ADR-0025:
/// <c>cap.forced &gt; org override &gt; tenant default &gt; cap.default</c>.
/// Writes audit rows atomically with every mutation so compliance teams can answer
/// "who turned hard-delete on and when?" without spelunking the erasure log.
/// </summary>
public sealed class DatabaseConfigurationResolver(
    TenantConfigDbContext dbContext,
    IComplianceCapProvider capProvider,
    ITenantContextAccessor tenantContextAccessor,
    ICacheService cache,
    ILogger<DatabaseConfigurationResolver> logger) : IConfigurationResolver
{
    private static readonly CacheOptions ResolverCacheOptions = new()
    {
        L1Ttl = TimeSpan.FromMinutes(2),
        L2Ttl = TimeSpan.FromMinutes(15)
    };

    /// <summary>
    /// Suffix appended to the Reason of a rejected <c>cap.Forced</c> override attempt so
    /// operators (and tests) can distinguish forced-cap rejections from genuine override
    /// writes. Exposed as a constant so production and test code share one source of truth.
    /// </summary>
    public const string CapForcedRejectedSuffix = "[rejected: cap.Forced]";

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var (_, orgId) = ResolveContextIds();

        // Only the effective value is cached — ResolvedConfiguration<T> (diagnostic view
        // for the admin panel) bypasses the cache. DaprCacheService prepends the tenant
        // prefix automatically via ITenantContextAccessor, so we only scope by org + key.
        var resolvedValueWrapper = await cache.GetOrSetAsync<CachedResolution<T>>(
            BuildCacheKey(key, orgId),
            async innerCt =>
            {
                var resolved = await ResolveAsync<T>(key, orgId, innerCt);
                return new CachedResolution<T>(resolved.Effective);
            },
            ResolverCacheOptions,
            ct);

        return resolvedValueWrapper.Value;
    }

    /// <inheritdoc />
    public async Task<ResolvedConfiguration<T>> GetResolvedAsync<T>(
        string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var (_, orgId) = ResolveContextIds();
        return await ResolveAsync<T>(key, orgId, ct);
    }

    private async Task<ResolvedConfiguration<T>> ResolveAsync<T>(
        string key, Guid? orgId, CancellationToken ct)
    {
        var cap = await capProvider.GetCapAsync(key, ct);

        var tenantEntry = await dbContext.Configurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key, ct);
        var tenantPresent = tenantEntry is not null;
        var tenantDefault = tenantPresent ? Deserialize<T>(tenantEntry!.Value, key) : default;

        var orgPresent = false;
        T? orgOverride = default;
        if (orgId is { } org)
        {
            var orgEntry = await dbContext.OrgOverrides
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.OrganizationId == org && c.Key == key, ct);
            if (orgEntry is not null)
            {
                orgPresent = true;
                orgOverride = Deserialize<T>(orgEntry.Value, key);
            }
        }

        // Precedence: cap.forced > org > tenant > cap.default.
        // `is not null` checks below only tell us the reference exists; value-type
        // emptiness is tracked via the explicit *Present flags so a genuine `false` / `0`
        // isn't confused with "unset".
        T? effective;
        ResolutionLayer winner;

        if (cap.Forced && cap.Value is not null)
        {
            effective = Deserialize<T>(cap.Value, key);
            winner = ResolutionLayer.Cap;
        }
        else if (orgPresent)
        {
            effective = orgOverride;
            winner = ResolutionLayer.OrgOverride;
        }
        else if (tenantPresent)
        {
            effective = tenantDefault;
            winner = ResolutionLayer.TenantDefault;
        }
        else if (cap.Value is not null)
        {
            effective = Deserialize<T>(cap.Value, key);
            winner = ResolutionLayer.Cap;
        }
        else
        {
            effective = default;
            winner = ResolutionLayer.None;
        }

        logger.LogDebug(
            "Resolved config key {Key} for org {OrgId}: winner={Layer}",
            key, orgId, winner);

        return new ResolvedConfiguration<T>(effective, tenantDefault, orgOverride, cap, winner);
    }

    /// <inheritdoc />
    public async Task SetOrgOverrideAsync<T>(
        string key, T value, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(value);
        if (reason.Length > 500)
            throw new ArgumentException("Reason must be 500 characters or fewer.", nameof(reason));

        var (tenantId, orgId) = ResolveContextIds();
        if (orgId is null)
            throw new InvalidOperationException(
                "Cannot set an org override without an organization in the current context.");
        var userId = ResolveChangedByUserId();

        var cap = await capProvider.GetCapAsync(key, ct);
        if (!cap.Allowed)
        {
            // Mirror the cap.Forced rejection path — record an audit row so the
            // forensic trail captures blocked attempts as well.
            AppendAudit(new AuditContext(
                tenantId, orgId, key,
                OldValue: null, NewValue: JsonSerializer.Serialize(value),
                ChangedByUserId: userId,
                Reason: $"{reason} {CapBlockedRejectedSuffix}"));
            await dbContext.SaveChangesAsync(ct);
            await InvalidateCacheAsync(key, orgId, ct);

            logger.LogWarning(
                "Org override rejected for {Key} (tenant {TenantId}, org {OrgId}) — platform cap disallows it",
                key, tenantId, orgId);
            throw new ComplianceCapViolationException(
                key, "lockey_identity_error_compliance_cap_blocks_override",
                isForced: cap.Forced, forcedValue: cap.Value, allowed: cap.Allowed);
        }
        if (cap.Forced)
        {
            // Forced caps collapse org overrides. Record the attempt (so the auditor sees
            // someone tried) but surface the block to the caller — returning silently
            // would misreport 200 OK while the override never took effect.
            AppendAudit(new AuditContext(
                tenantId, orgId, key,
                OldValue: null, NewValue: JsonSerializer.Serialize(value),
                ChangedByUserId: userId,
                Reason: $"{reason} {CapForcedRejectedSuffix}"));
            await dbContext.SaveChangesAsync(ct);
            await InvalidateCacheAsync(key, orgId, ct);

            logger.LogWarning(
                "Org override rejected for {Key} (tenant {TenantId}, org {OrgId}) — platform cap is Forced",
                key, tenantId, orgId);
            throw new ComplianceCapViolationException(
                key, "lockey_identity_error_compliance_cap_blocks_override",
                isForced: true, forcedValue: cap.Value, allowed: cap.Allowed);
        }

        var json = JsonSerializer.Serialize(value);
        var existing = await dbContext.OrgOverrides
            .FirstOrDefaultAsync(c => c.OrganizationId == orgId.Value && c.Key == key, ct);
        var oldJson = existing?.Value;

        if (existing is null)
        {
            dbContext.OrgOverrides.Add(new OrgConfigEntry
            {
                OrganizationId = orgId.Value,
                Key = key,
                Value = json,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = userId.ToString()
            });
        }
        else
        {
            existing.Value = json;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedBy = userId.ToString();
        }

        AppendAudit(new AuditContext(
            tenantId, orgId, key,
            OldValue: oldJson, NewValue: json,
            ChangedByUserId: userId, Reason: reason));
        await dbContext.SaveChangesAsync(ct);

        await InvalidateCacheAsync(key, orgId, ct);

        // NO PII rule (see permissions.md / observability standard): Reason, raw values,
        // and ChangedByUserId stay in the audit table only; logs carry scope + key only.
        logger.LogInformation(
            "Org override saved for {Key} (tenant {TenantId}, org {OrgId})",
            key, tenantId, orgId);
    }

    /// <summary>Suffix appended to rejected <c>cap.Allowed=false</c> attempts in the audit trail.</summary>
    public const string CapBlockedRejectedSuffix = "[rejected: cap.Allowed=false]";

    /// <inheritdoc />
    public async Task ClearOrgOverrideAsync(
        string key, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 500)
            throw new ArgumentException("Reason must be 500 characters or fewer.", nameof(reason));

        var (tenantId, orgId) = ResolveContextIds();
        if (orgId is null)
            throw new InvalidOperationException(
                "Cannot clear an org override without an organization in the current context.");
        var userId = ResolveChangedByUserId();

        var existing = await dbContext.OrgOverrides
            .FirstOrDefaultAsync(c => c.OrganizationId == orgId.Value && c.Key == key, ct);

        if (existing is null)
        {
            logger.LogDebug(
                "Clear-override no-op: no org override exists for {Key} in org {OrgId}",
                key, orgId);
            return;
        }

        var oldJson = existing.Value;
        dbContext.OrgOverrides.Remove(existing);

        AppendAudit(new AuditContext(
            tenantId, orgId, key,
            OldValue: oldJson, NewValue: null,
            ChangedByUserId: userId, Reason: reason));
        await dbContext.SaveChangesAsync(ct);

        await InvalidateCacheAsync(key, orgId, ct);

        logger.LogInformation(
            "Org override cleared for {Key} (tenant {TenantId}, org {OrgId})",
            key, tenantId, orgId);
    }

    private (Guid TenantId, Guid? OrgId) ResolveContextIds()
    {
        var ctx = tenantContextAccessor.Current;
        var tenantId = ctx.TryGetTenantGuid()
            ?? throw new InvalidOperationException("Configuration resolver requires a tenant context.");
        var orgId = ctx.TryGetOrganizationGuid();
        return (tenantId, orgId);
    }

    private Guid ResolveChangedByUserId()
    {
        var userId = tenantContextAccessor.Current.UserId;
        if (!Guid.TryParse(userId, out var guid) || guid == Guid.Empty)
            throw new InvalidOperationException(
                "Configuration writes require an authenticated user in the tenant context.");
        return guid;
    }

    private void AppendAudit(AuditContext ctx)
    {
        dbContext.PolicyAudit.Add(new CompliancePolicyAuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.TenantId,
            OrganizationId = ctx.OrganizationId,
            Key = ctx.Key,
            OldValue = ctx.OldValue,
            NewValue = ctx.NewValue,
            ChangedByUserId = ctx.ChangedByUserId,
            ChangedAtUtc = DateTimeOffset.UtcNow,
            Reason = ctx.Reason
        });
    }

    /// <summary>
    /// Parameter-bag record for <see cref="AppendAudit"/> — keeps the helper's signature
    /// stable even as audit fields grow. Internal to the resolver; not exposed.
    /// </summary>
    private sealed record AuditContext(
        Guid TenantId,
        Guid? OrganizationId,
        string Key,
        string? OldValue,
        string? NewValue,
        Guid ChangedByUserId,
        string Reason);

    private async Task InvalidateCacheAsync(string key, Guid? orgId, CancellationToken ct)
    {
        await cache.RemoveAsync(BuildCacheKey(key, orgId), ct);
    }

    private static string BuildCacheKey(string key, Guid? orgId)
        => $"config:resolved:{orgId?.ToString() ?? "_"}:{key}";

    // Wrapper exists because DaprCacheService.GetOrSetAsync<T> requires a reference type
    // for nullable-safe caching; raw nullable value types would round-trip as boxed defaults.
    private sealed record CachedResolution<T>(T? Value);

    private T? Deserialize<T>(string? json, string key)
    {
        if (string.IsNullOrEmpty(json))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize configuration for Key {Key} (RawLength={RawLength})",
                key, json.Length);
            return default;
        }
    }
}
