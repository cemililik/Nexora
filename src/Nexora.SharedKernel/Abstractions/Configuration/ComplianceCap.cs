namespace Nexora.SharedKernel.Abstractions.Configuration;

/// <summary>
/// Platform-level cap applied to a compliance configuration key. Caps are defined by the
/// operator (via NMP in SaaS deployments, via the signed license key in on-prem) and
/// constrain what tenants / organizations may set as their own value.
/// </summary>
/// <param name="Allowed">
/// When <see langword="false"/>, organizations MUST NOT enable the key — org overrides
/// that attempt to enable it are rejected by the resolver.
/// </param>
/// <param name="Forced">
/// When <see langword="true"/>, the cap's own value wins regardless of tenant default
/// or org override. Used for mandatory platform policies (e.g. EU tenants where
/// hard-delete is enforced).
/// </param>
/// <param name="Value">
/// The cap's own effective value. Used when <see cref="Forced"/> is <see langword="true"/>
/// or when no lower tier (tenant, org) supplies a value. May be <see langword="null"/>
/// when the cap carries no default and simply constrains the range.
/// </param>
/// <remarks>
/// See ADR-0025 (Org-Scoped Compliance Config with Platform-Level Caps) for full
/// precedence semantics: <c>cap.forced &gt; org &gt; tenant &gt; cap.default</c>.
/// </remarks>
public sealed record ComplianceCap(bool Allowed, bool Forced, string? Value = null)
{
    /// <summary>
    /// A fully permissive cap — no restriction, no forced value. Used by
    /// <c>NullComplianceCapProvider</c> in dev and on-prem deployments where no
    /// platform-level policy authority exists yet.
    /// </summary>
    public static readonly ComplianceCap Permissive = new(Allowed: true, Forced: false, Value: null);
}
