# 0025 — Organization-scoped compliance config with platform-level policy caps

## Status

Accepted

## Date

2026-04-23 (Proposed), 2026-04-23 (Accepted by maintainer)

## Context

Nexora currently stores per-tenant configuration in `platform_tenant_config` (a
`(Key, Value, UpdatedAt)` KV table inside each tenant schema) and resolves it through
`ITenantConfiguration`/`DatabaseTenantConfiguration`. The first real consumer of this store
is the GDPR hard-delete feature gate — `gdpr.hard_delete.enabled` — read by
[`RequestGdprDeleteHandler`](../../src/Modules/Nexora.Modules.Contacts/Application/Commands/RequestGdprDeleteCommand.cs)
to decide between anonymize-in-request (Mod A) and `GdprHardDeleteJob` enqueue (Mod B).
Today the flag is **tenant-global**: one switch applies to every organization under the
tenant. ADR-008 and T-004 already declared hard-delete the target compliance posture for
Phase 1.5.6.

Three structural forces now push against keeping this flag tenant-global:

1. **Multi-org tenants span jurisdictions.** A single tenant frequently carries orgs in
   different legal regimes (EU/GDPR vs. US/CCPA vs. TR/KVKK). A blanket tenant-wide
   hard-delete policy forces the strictest jurisdiction's choice on every org — or leaves
   a non-GDPR org running under a policy it cannot justify to its own auditors.
2. **Tenant-level management is moving to NMP (ADR-0023 / MANAGEMENT_PORTAL.md).** The
   admin panel will no longer expose tenant-scope toggles after NMP.3 ships. Anything
   that remains user-configurable in the admin panel has to be **org-scoped** by
   construction, otherwise it has nowhere to live post-migration.
3. **GDPR hard-delete is a destructive, regulated capability.** Letting any admin with
   `contacts.gdpr.delete` also flip the policy is a separation-of-duties violation. The
   person who operates an erasure must not also be the person who decides whether the
   erasure is reversible.

The existing `platform_tenant_config` has no `OrganizationId` column, and
`ITenantConfiguration.GetAsync<T>(key, ct)` has no organization parameter — the tenant is
resolved implicitly from `ITenantContextAccessor`. Extending this without breaking the
Phase-1.5.6 schedule (T-004 in review) and without pulling NMP into Phase 1 territory is
the central constraint.

## Decision drivers

- Org admins must be able to set org-specific compliance posture (GDPR hard delete,
  future: retention windows, audit verbosity, export controls) for just their org.
- Tenant-wide "floor" must remain enforceable by the platform operator — an org must
  never be able to **loosen** compliance beyond what the platform policy permits.
- No breaking change to existing callers: `ITenantConfiguration.GetAsync<T>(key)` must
  keep working; org scope is additive.
- Toggling the policy itself is a sensitive action — separate permission, separate
  audit trail, no accidental exposure to `contacts.gdpr.delete` holders.
- NMP will own the platform-level cap surface in the SaaS deployment model. In on-prem
  the cap is set by the local Platform Admin (or left open). The SaaS runtime must work
  before NMP ships (Phase 1.5) and hand cleanly to NMP after NMP.1.
- Resolution must be O(1) on the read path — GDPR handlers are already on the
  request path for delete operations and can't afford a multi-query lookup per call.

## Considered options

1. **Option A — 3-tier resolver: Platform cap → Tenant default → Org override (chosen)**

   Three storage surfaces, one resolver:

   - **Platform cap** (read-only from the tenant side). Lives in the `platform_license_cache`
     entitlements JSON maintained by NMP (SaaS) or the signed license key (on-prem). Expressed
     as a `ComplianceCap` object:
     `{ "gdpr.hard_delete.enabled": { "allowed": true|false, "forced": true|false, "value": "true"|null } }`
     where `allowed=false` means an org CANNOT turn it on, `forced=true` means an org
     CANNOT turn it off, and `value` carries the serialized value the cap pins
     (read when `forced=true`, or used as the platform-supplied default when both
     tenant and org layers are empty). `value` MAY be omitted / `null` when the cap
     only gates the *ability* to override. `NullComplianceCapProvider` returns the
     explicit `(allowed: true, forced: false, value: null)` tuple in dev — never
     implicit defaults — so every caller sees the same shape.
   - **Tenant default**. Remains in `platform_tenant_config` (tenant schema). Same shape as
     today: `(Key, Value, UpdatedAt)`. Org admins read-through when they have no override.
   - **Org override**. New table `platform_org_config` in the tenant schema:
     `(OrganizationId, Key, Value, UpdatedAt, UpdatedBy)` — composite key
     `(OrganizationId, Key)`. Only managed by org admins via the admin panel.

   Resolution (`IConfigurationResolver.GetAsync<T>(key, ct)`):
   1. Read platform cap from license entitlements.
   2. Read tenant default.
   3. Read org override (scoped by `ITenantContextAccessor.OrganizationId`).
   4. Compute effective value: org override wins if present AND cap allows; else tenant
      default clamped by cap; else cap default.
   5. If cap.forced, cap value wins regardless.

   L1 in-memory cache (2 min) keyed by `{tenantId, orgId, key}` — already the pattern
   for `ICacheService`, so we can reuse it.

   **Pros**
   - Separation of concerns: operator cap, tenant default, org override — each layer
     has a clear owner and audit surface.
   - NMP-ready without requiring NMP: in dev and on-prem, the cap layer is a no-op;
     in SaaS, NMP publishes caps via the existing license-verify channel.
   - Additive on storage: existing `platform_tenant_config` untouched.
   - Matches ADR-0016 / ADR-0017 permission scope model (`Platform` vs `Tenant`).

   **Cons**
   - Three storage locations to read on a cache miss.
   - Policy change audit is spread across org, tenant, and NMP surfaces — needs a
     unified view for compliance teams.

2. **Option B — Single table with `OrganizationId NULL` for tenant default**

   Add `OrganizationId uuid NULL` to `platform_tenant_config`. NULL row = tenant default;
   non-NULL = org override. Resolver: `SELECT value WHERE key=? AND (org_id=? OR org_id IS NULL)
   ORDER BY org_id NULLS LAST LIMIT 1`.

   - Pros: minimal schema change, one query.
   - Cons: no platform cap layer — tenant admin can always override "down" from org — so
     the separation-of-duties requirement fails. Also couples the platform cap to tenant
     storage, which contradicts the NMP direction (caps should live with licensing).

3. **Option C — Hardcoded platform policy, no org scope**

   Keep tenant-global flag. Platform policy shipped as appsettings / compile-time.

   - Pros: zero schema change.
   - Cons: multi-org jurisdiction problem unsolved; tenant-global flag migrates nowhere
     clean when admin panel drops tenant toggles.

## Decision outcome

**Option A**. Three-tier resolver with platform cap → tenant default → org override.

Reasoning anchored to the drivers:

- The multi-org jurisdiction driver is only served by a real org scope (Options A, B).
- The separation-of-duties + NMP-ownership drivers eliminate Option B (no cap layer).
- The "NMP not required in Phase 1.5" driver is met by letting `NullComplianceCapProvider`
  return a permissive cap until NMP.1 is deployed — the resolver already has a cap
  layer so wiring NMP in later is a single impl swap, not a schema migration.

Implementation lands in Phase 1.5.6 alongside T-004 (hard-delete). T-004's runtime
behaviour does not change — it still reads `gdpr.hard_delete.enabled` — but the value
source moves from `ITenantConfiguration` to the new `IConfigurationResolver`.

## Consequences

### Positive

- Per-org compliance policy for multi-jurisdictional tenants.
- Platform operator keeps a hard cap (no org can silently disable hard-delete where NMP
  requires it for EU tenants; no tenant admin can override a platform force-enable).
- Separation of duties: new permission `contacts.gdpr.settings_manage` gates toggling;
  existing `contacts.gdpr.delete` continues to gate executing. Holders of one do not
  automatically hold the other.
- Admin panel survives the NMP migration: tenant toggles were never the intended long-term
  surface; only org toggles remain, which is where they always belonged.
- Pattern is reusable: retention windows, audit verbosity, data-residency flags — every
  future compliance knob uses the same cap/default/override structure.

### Negative

- Three read surfaces, one more table (`platform_org_config`), one more cache key
  shape (org-scoped, not just tenant-scoped).
- All existing `ITenantConfiguration` callers need to pass an org context eventually.
  During transition (Phase 1.5.6) the shim keeps backward compatibility by defaulting
  org to the accessor's current org, but new code should call `IConfigurationResolver`
  directly.
- Operational: NMP has to expose a "compliance caps" editor — not trivial, but aligns
  with the NMP.2 billing/entitlements editor already on the roadmap.

### Neutral

- `ITenantConfiguration` becomes a thin wrapper over `IConfigurationResolver` for
  backward compatibility. Eventually deprecated.
- Dev and on-prem keep working without NMP thanks to `NullComplianceCapProvider`
  returning `ComplianceCap.Permissive`.

## Implementation notes

- **New table (tenant schema):** `platform_org_config`
  `(OrganizationId uuid, "Key" varchar(256), "Value" jsonb NOT NULL, "UpdatedAt" timestamptz,
  "UpdatedBy" varchar(200) NULL, PRIMARY KEY (OrganizationId, "Key"))`. Added via
  `DevelopmentSeed.ApplySchemaUpdatesAsync` per the existing migration-free pattern
  (docs/standards/schema-migration.md).
- **New policy-audit table (tenant schema):** `platform_compliance_policy_audit`
  `(Id uuid PK, TenantId uuid, OrganizationId uuid NULL, "Key" varchar(256), OldValue jsonb,
  NewValue jsonb, ChangedByUserId uuid, ChangedAtUtc timestamptz, Reason varchar(500) NULL)`.
  Every write through `IConfigurationResolver.SetOrgOverrideAsync` /
  `ClearOrgOverrideAsync` creates an audit row atomically with the update in the same
  transaction.
- **New permission:** `contacts.gdpr.settings_manage` (tenant-scope, seeded by the
  Identity module via `IPermissionRegistry` — underscore keeps the canonical
  `{module}.{resource}.{action}` three-part form per permissions.md §1). Granted to
  Platform Admin by default; **not** granted to Tenant User.
- **New permission (platform-scope):** `platform.compliance.policy_manage` — NMP-side,
  controls who can set caps. Scope = `Platform` per ADR-0016 permission-tier model.
- **Interface additions in `Nexora.SharedKernel.Abstractions.Configuration`:**

  ```csharp
  public interface IConfigurationResolver
  {
      Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
      Task SetOrgOverrideAsync<T>(string key, T value, string reason, CancellationToken ct = default);
      Task ClearOrgOverrideAsync(string key, string reason, CancellationToken ct = default);
      Task<ResolvedConfiguration<T>> GetResolvedAsync<T>(string key, CancellationToken ct = default);
  }

  // `Value` carries the cap's own serialized value when `Forced=true` (it wins the
  // precedence ladder) OR as a last-resort default when no tenant/org layer supplies
  // one. `null` means the cap only constrains whether an override may be set (the
  // common case). `ComplianceCap.Permissive = (Allowed: true, Forced: false, Value: null)`
  // is returned by `NullComplianceCapProvider` in dev / on-prem pre-NMP.
  public sealed record ComplianceCap(bool Allowed, bool Forced, string? Value = null);

  public interface IComplianceCapProvider
  {
      Task<ComplianceCap> GetCapAsync(string key, CancellationToken ct = default);
  }
  ```
- **`ComplianceCap` sources:**
  - `NullComplianceCapProvider` (dev, on-prem pre-NMP) — always returns the explicit
    tuple `(Allowed=true, Forced=false, Value=null)` via `ComplianceCap.Permissive`.
  - `NmpComplianceCapProvider` (SaaS, NMP.1+) — reads from the `platform_license_cache.EntitlementsJson`
    under a `compliance.caps` sub-object. NMP publishes caps via the existing
    `PUT /api/internal/tenants/{id}/entitlements` endpoint — no new wire contract needed.
- **Backward compatibility:** `ITenantConfiguration` keeps its existing method signature
  *and* its existing behavior — `DatabaseTenantConfiguration` reads `platform_tenant_config`
  directly (no org layer, no cap check). Delegating the shim to `IConfigurationResolver`
  was considered and rejected: legacy keys that are NOT yet cap-managed must keep their
  pre-ADR semantics exactly, otherwise every existing consumer silently picks up the
  three-tier resolution and any cap-provider misconfiguration becomes a tenant-wide
  regression. Migration is per-key: when a key graduates to org-scope management, its
  call site switches to `IConfigurationResolver` in the same PR.
- **Admin panel UI:** new page under `/identity/settings/compliance` — lists each
  compliance key, shows (tenant default | org override | effective value) with cap badge
  (🔒 forced, 🚫 blocked, ⚙️ configurable). Requires `contacts.gdpr.settings_manage`.
- **NMP UI:** new "Compliance caps" section under tenant detail page (NMP.2 milestone
  extension). Requires `platform.compliance.policy_manage`.
- **Rollout:**
  1. Phase 1.5.6 T-019 (this ADR's implementation task): resolver, new tables, admin UI,
     `contacts.gdpr.settings_manage` permission, `NullComplianceCapProvider`. T-004's
     `RequestGdprDeleteHandler` reads through `IConfigurationResolver` — feature flag
     semantics unchanged for existing users.
  2. NMP.1 (parallel track): `NmpComplianceCapProvider` + NMP compliance caps editor.
  3. Future compliance knobs (retention windows, residency, etc.) reuse the pattern.
- **Observability:**
  - Metric: `nexora_compliance_config_resolution_count{layer=cap|tenant|org}` — counts which layer won.
  - Metric: `nexora_compliance_policy_changes_total{key,scope}` — policy change rate.
  - Log (`Information`): every `SetOrgOverrideAsync` with `{Key, TenantId, OrgId}` plus the
    policy-audit row id so operators can join to the forensic trail. The free-text `Reason`
    and the raw `OldValue`/`NewValue` stay in the audit table only — never in application
    logs. `ChangedByUserId` is captured in audit and omitted from logs per the observability
    standard's no-PII rule.
- **Testing:**
  - Unit: resolver precedence (cap.forced > org > tenant > cap.default).
  - Unit: policy audit row written for every write.
  - Integration: two orgs under one tenant — override visibility isolated per org.
  - Integration: cap.allowed=false prevents org override; cap.forced=true makes org
    override a no-op.
  - Architecture test: `platform_tenant_config` is never read directly outside
    `IConfigurationResolver` — future keys must go through the resolver.

## References

- ADR-008 — GDPR deletion strategy (hard-delete target for Phase 1.5.6)
- ADR-0016 — Module tier classification (permission scope model)
- ADR-0017 — Portal extension architecture (cap flow via entitlements)
- ADR-0023 — NMP billing model (license-cache channel reused for caps)
- `docs/architecture/MANAGEMENT_PORTAL.md` — NMP compliance-caps editor section
- `docs/standards/schema-migration.md` — migration-free schema evolution pattern used here
- `docs/standards/permissions.md` — `{module}.{resource}.{action}` naming
- T-004 — GDPR Article 17 hard delete (In Review; this work is its runtime gate)
- T-019 — Implementation of this ADR (org-scoped config + caps)

## Amendment 1 — `cap.Allowed=false` short-circuits lower layers

**Date:** 2026-04-23

**What:** When `IComplianceCapProvider.GetCapAsync(...)` returns
`Allowed = false`, `IConfigurationResolver` resolves to `cap.Value`
(or `default(T)` when `cap.Value` is null) and **skips the org-override
and tenant-default layers entirely**. The pre-amendment behaviour read
the lower layers first and only blocked *writes* via the cap; this
allowed a stale org override left over from before a cap tightened to
keep leaking through reads.

**Why:** The original three-tier ladder `cap.forced > org > tenant >
cap.default` only honoured the cap on writes (`SetOrgOverrideAsync`)
when `Allowed = false`. Reads of an existing override from before the
cap was tightened still returned the old value — a real correctness
gap in the EU-tenant compliance flow that ADR-0023 gates on. The
amended ladder is `cap.forced > (cap.blocked → cap.value) > org >
tenant > cap.default`: when the cap blocks, neither org nor tenant
contribute.

**Consequence for callers:**

- `cap.Allowed = false` with `cap.Value = null` resolves to
  `default(T)` — for `bool` that is `false`, for `int?` that is `null`,
  for reference types `null`. Document call sites that depend on the
  defaulted value (T-019's `gdpr.hard_delete.enabled` reads as
  `false` under a forced-disable cap, which is the intended outcome).
- A tenant whose cap tightens does not need a manual purge of stale org
  overrides — the resolver stops returning them at the next read. The
  audit table still preserves the override row for forensic purposes;
  the resolver simply doesn't surface it.

**Test coverage:**
`Get_CapAllowedFalse_IgnoresLowerLayers_EvenIfPresent` +
`SetOrgOverride_CapDisallows_AuditRecordsPriorOverrideAsOldValue`
in `tests/Nexora.Infrastructure.Tests/Configuration/DatabaseConfigurationResolverTests.cs`
— both green from commit `576ff65`.

**Scope:** This is a clarification of behaviour the original ADR
described but did not specify in normative terms. No code change is
required by this amendment; the change shipped in `576ff65` and this
amendment captures the reasoning for future readers.
