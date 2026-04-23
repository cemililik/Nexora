# NMP Track — Nexora Management Portal

**Type:** Parallel track (not a numbered phase) — runs concurrently alongside Phases 2 → 4.
**Starts:** Phase 1.5 week 4, after the Permission Tier System (Phase 1.5.2) lands.
**Architecture:** [`../../architecture/MANAGEMENT_PORTAL.md`](../../architecture/MANAGEMENT_PORTAL.md)
**Status (2026-04-22):** Planned — not started.

## Why this is a track and not a phase

NMP is the platform-operator product (tenant lifecycle, licensing, billing, marketplace). It is
**not** a tenant-facing module, ships from a separate codebase (`Nexora.Management`), and needs
to run in parallel with several numbered phases:

- Phase 2 — NMP billing integrates with Subscription module.
- Phase 2.5 — HR Core is a priced license tier surface.
- Phase 3a / 3b — NGO and Education Editions are priced add-ons.
- Phase 4 — Extensions are marketplace SKUs.

Embedding NMP inside any single phase would misrepresent its scope and freeze it to that phase's
exit bar. ADR-015 (Roadmap Structure) permits tracks alongside phase files for exactly this
parallelism pattern.

## Exit bar

1. `Nexora.Management` solution exists; NMP operators log in via `nexora-management` Keycloak
   realm; can create / list / suspend tenants.
2. `NmpLicenseVerifier` replaces `NullLicenseVerifier` in SaaS; all Tier-2+ module installs
   route through `POST /internal/license/verify` before any tenant can enable them.
3. Stripe billing is live for SaaS tenants: plan create / upgrade / cancel, webhook-driven
   invoice and payment-attempt ingestion.
4. Admin panel no longer exposes tenant CRUD; tenants can see their own license tab showing
   plan, module entitlements, and renewal state.
5. On-prem / marketplace path exists: RSA-signed license keys, phone-home default with manual
   fallback, 30-day grace period on expiry.

## Scope

- Centralized tenant lifecycle management (create / suspend / terminate / status audit).
- License definition: `Plan`, `LicenseKey`, `ModuleCatalog` entities in the NMP codebase.
- License verification API consumed by the Nexora runtime at module-install time.
- Stripe-backed SaaS billing; hybrid phone-home + RSA-signed license for on-prem.
- Operator portal frontend (tenant dashboard, module catalog, license issuance).
- Module marketplace catalog UI (buy / enable / install per tenant).
- Removal of tenant CRUD from the tenant admin panel.

## Out of scope

- Tenant-facing subscription billing (that is Tier-2 Subscription module for the tenant's own
  customers — different product, different codebase).
- Finance GL posting inside NMP — NMP billing events are consumed by the SaaS Finance module
  the same way tenant-owned Subscription events are.
- Per-tenant permission management (lives in Identity / Permission Tier System, Phase 1.5.2).

## Dependencies

- **Phase 1.5.2** must have shipped: `PermissionScope` enum, `ILicenseVerifier` interface,
  `NullLicenseVerifier` default, `platform_license_cache` table. NMP replaces the verifier
  with a real implementation; without 1.5.2 there is no socket to plug into.
- **Phase 2** for NMP.3 (admin panel adaptation): the tenant license tab cannot render until
  Tier-2 modules exist to gate.

## Milestones

Four milestones, roughly 4 weeks each; run concurrently with Phase 2 development.

### NMP.1 — Foundation (weeks 1–4)

Prerequisite: Phase 1.5.2 Permission Tier System complete.

- [ ] Create `Nexora.Management` solution (separate codebase, own repo or monorepo sub-tree).
- [ ] Keycloak `nexora-management` realm for platform operators.
- [ ] `Subscription`, `LicenseKey`, `ModuleCatalog` domain entities in NMP.
- [ ] Tenant lifecycle API (create / list / status management) callable from the operator portal.
- [ ] License verification internal endpoint: `POST /internal/license/verify`.
- [ ] `NmpLicenseVerifier` implementation in the SaaS runtime; replaces `NullLicenseVerifier`
      without touching modules that depend on `ILicenseVerifier`.

### NMP.2 — Billing Integration (weeks 5–8)

- [ ] Stripe subscription management (create / update / cancel) driving NMP `Subscription`
      entity.
- [ ] `Invoice` entity + Stripe webhook handlers (invoice issued, paid, payment failed).
- [ ] Plan upgrade / downgrade flows (reuse the proration policy documented in the tenant
      Subscription module spec where applicable — but note these are **different entities in
      different codebases**; see ADR-0019 for the analogous tenant-Subscription vs Fundraising
      separation).
- [ ] NMP frontend: tenant dashboard, subscription management, invoice history.
- [ ] **Compliance caps editor (ADR-0025)** — NMP UI to set/clear `compliance.caps.<key>`
      entries (`allowed`, `forced`, `value`) per tenant. Required platform permission:
      `platform.compliance.policy_manage`. Published via the existing
      `PUT /api/internal/tenants/{id}/entitlements` channel under the
      `compliance.caps` sub-object — no new wire contract.
- [ ] **`NmpComplianceCapProvider`** — SaaS CRM runtime implementation of
      `IComplianceCapProvider` (SharedKernel). Reads caps from `platform_license_cache.EntitlementsJson`
      at `compliance.caps.<key>`. Replaces `NullComplianceCapProvider` (shipped in T-019)
      via DI swap in SaaS deployments. On-prem stays on `NullComplianceCapProvider` or a
      future `LicenseKeyCapProvider` (NMP.4) that reads caps from the signed license key.
- [ ] **Compliance-resolver metrics** (deferred from T-019 intentionally) — labels match
      ADR-0025:
  - `nexora_compliance_config_resolution_count{layer=cap|tenant|org}` — counts which layer
    won, per key.
  - `nexora_compliance_policy_changes_total{key,scope}` — policy-change rate for SLO tracking.
  - Emission happens inside `DatabaseConfigurationResolver`; the metric schema change lands
    in this phase, not Phase 1.5, because NMP needs to expose the same counters in its own
    analytics dashboard for operator visibility.

### NMP.3 — Admin Panel Adaptation (after Phase 2 modules exist)

Prerequisite: Phase 2 modules exist so the license tab has real content to display.

- [ ] Remove tenant CRUD from the tenant admin panel.
- [ ] License-aware module installation: runtime queries `ILicenseVerifier` before any Tier-2+
      module install completes.
- [ ] License tab in admin settings (plan, limits, modules, renewal, upgrade link).
- [ ] Purchase / upgrade redirect flow from admin panel to NMP portal.
- [ ] Usage-metric collection Hangfire job (per-tenant module usage → NMP for plan-limit
      checks and billing).

### NMP.4 — On-Prem & Marketplace (weeks 13–16)

- [ ] RSA-signed license key generation in NMP.
- [ ] Initial-setup license activation wizard in the tenant admin panel (first run on on-prem
      install).
- [ ] `LicenseKeyVerifier` implementation (validates RSA-signed key locally, on-prem path).
- [ ] Hybrid license model: phone-home by default + manual key as fallback.
- [ ] Air-gapped mode (no outbound network to NMP; relies purely on the signed key).
- [ ] Module marketplace catalog UI in NMP (browse / buy / enable Tier-4 extensions).
- [ ] 30-day grace period after license expiry → read-only mode; enforced by the verifier
      implementations.

## Acceptance criteria

- [ ] `NullLicenseVerifier` is the default only in Development; Production and Staging use
      `NmpLicenseVerifier` or `LicenseKeyVerifier` exclusively.
- [ ] Every Tier-2+ module install path calls `ILicenseVerifier.VerifyAsync()` with the module
      ID and tenant ID; failure blocks install.
- [ ] Tenant admin panel no longer shows a tenant-create form.
- [ ] Operator portal tenant table lists all tenants across all environments with status,
      plan, and next renewal.
- [ ] Stripe webhook → NMP `Invoice.Paid` → operator portal invoice history, round-trip
      tested.
- [ ] On-prem install with a signed license key works without any outbound network traffic.

## ADR ledger

- **Introduces:** none yet; an NMP billing-model ADR may be needed before NMP.2 to freeze the
  Stripe data-mapping decisions (TODO maintainer).
- **Consumes:** ADR-0015 (phase/track model), ADR-0016 (tier classification — licenses are
  scoped per tier), ADR-0017 (Portal Extension Architecture — marketplace installs use the
  same manifest as first-party modules), ADR-0018 (payment provider strategy — same Stripe /
  iyzico port is used on both sides of the product), ADR-0019 (recurring billing vs recurring
  donations — informs why NMP's `Subscription` entity and the tenant `Subscription` module
  are deliberately separate).

## Informs

- Phase 2 CRM / Subscription / Finance / Projects — all Tier-2 specs assume NMP license gating
  is real once NMP.1 ships.
- Phase 2.5 HR — license tier surface.
- Phase 3a / 3b Editions — pricing add-ons enforced by NMP.
- Phase 4 Extensions — marketplace fulfilment.

## Open questions / maintainer TODOs

- Single NMP codebase or monorepo sub-tree? (Affects CI pipeline and deploy model.)
- Stripe accounts: one global vs per-region? (TR tax registration implications for iyzico.)
- Operator portal i18n scope — English only or EN + TR like tenant portal?

## Related

- [`../current.md`](../current.md) — confirm `NMP.1` is surfaced as an in-flight initiative
  when work begins.
- [`../../architecture/MANAGEMENT_PORTAL.md`](../../architecture/MANAGEMENT_PORTAL.md).
- [`phase-1.5-bridge.md`](phase-1.5-bridge.md) §1.5.2 — the Permission Tier System this track
  depends on.
- [`phase-2-enterprise.md`](phase-2-enterprise.md) — parallel track.
