# ADR-017: Portal Extension Architecture

## Status
Accepted

## Date
2026-04-22

## Context

The Portal Framework (Phase 1.5, Next.js 16) and Admin Dashboard (React 19) are designed to host
UI contributed by feature modules. Today the contribution mechanism is partly convention, partly
ad-hoc: each module exposes a TypeScript `ModuleManifest` object imported and composed at build
time by the host application. This couples the host to every module's source, which blocks:

- **Per-tenant module installation.** A tenant who has not installed the CRM module should not
  ship CRM JavaScript to its users; today the bundle is fixed.
- **Marketplace extensions (Tier 4).** Third-party modules cannot be added without editing the
  host's import graph.
- **Vertical Editions (Tier 3a/3b).** Editions must be swappable per tenant; build-time imports
  make this impossible without separate deployments.
- **License gating.** Route, menu, and widget visibility must obey the tenant's license; there
  is no single place the framework can read this from.

We need a declarative extension contract that every Tier-2+ module publishes, and a runtime
mechanism by which the host loads extensions based on tenant module installations.

This ADR is **foundational**: Phase 2 will pilot it with one Tier-2 module (CRM or Subscription)
and the results feed back to the spec.

## Decision drivers

- Module ↔ host coupling must be data-driven, not import-driven.
- Manifest schema must cover routes, menu, widgets, permissions, i18n, and tier/license gates.
- Lazy loading is required: installing a module adds JS only on routes that need it.
- Contract must work identically for first-party (Tier 1–3) and marketplace (Tier 4) modules.
- Manifest must be versioned so future schema changes don't break installed modules.

## Considered options

### Option A — Keep build-time TypeScript imports
Status quo. Host imports each module's `manifest.ts` directly.
- (+) Simple; type-checked end-to-end.
- (−) Cannot gate by tenant install state; cannot support third-party modules.
- (−) Every module install/uninstall requires a host redeploy.

### Option B — Runtime manifest fetched from backend API
Each module emits a YAML/JSON manifest at build time; backend assembles an "install-set" per
tenant; host fetches `/api/v1/portal/manifests` at boot and lazy-loads module bundles.
- (+) Decouples host from modules; per-tenant install state is natural.
- (+) Single schema; works for first- and third-party.
- (−) One more network roundtrip at portal boot.
- (−) Bundle-splitting strategy needs care (per-module chunks).

### Option C — Module federation (Webpack 5 / Rspack)
Use framework-level module federation to load modules as remote containers at runtime.
- (+) True runtime loading, including from external origins.
- (−) Significant infra complexity; Next.js 16 support for federation is still maturing.
- (−) Overkill for the current scale.

## Decision outcome

**Chosen: Option B — declarative YAML manifest + runtime assembly + lazy chunks.**

### `module.manifest.yaml` schema (v1)

Every Tier-2+ module publishes a `module.manifest.yaml` alongside its source. Tier-1 modules
may opt in (Portal Framework, Admin Dashboard manifests are implicit host shells).

```yaml
manifestVersion: 1
module:
  id: crm                         # matches backend module ID; kebab-case
  name: lockey_crm_module_name    # i18n key, resolved by portal
  tier: 2                         # from ADR-016
  version: 1.0.0                  # semver
  dependencies:                   # other modules that must be installed
    - contacts
    - notifications
  license:                        # Tier 2+ license gate
    requires: enterprise          # one of: none | enterprise | ngo | education | extension
    featureFlag: crm.enabled      # optional; fine-grained toggle

routes:
  - path: /crm/leads
    component: ./pages/LeadListPage            # lazy-loaded chunk
    permission: crm.leads.read
    layout: portal                             # portal | admin | public
  - path: /crm/leads/:id
    component: ./pages/LeadDetailPage
    permission: crm.leads.read

menu:
  - section: sales
    label: lockey_crm_menu_leads
    icon: users
    route: /crm/leads
    order: 100
    permission: crm.leads.read

widgets:
  - id: crm.pipeline-summary
    slot: dashboard.main
    component: ./widgets/PipelineSummary
    permission: crm.pipelines.read
    order: 20

permissions:
  namespace: crm
  declared:
    - crm.leads.read
    - crm.leads.write
    - crm.pipelines.read
    - crm.pipelines.write

i18n:
  namespace: crm
  locales: [en, tr]
  path: ./locales

entryPoints:
  esm: ./dist/crm.esm.js          # served at /_modules/crm/crm.esm.js
  integrity: sha384-...           # SRI hash for Tier 4 marketplace modules
```

### Runtime loading flow

1. **Boot.** Portal (or Admin) calls `GET /api/v1/portal/manifests` with the tenant's session.
2. **Backend assembles** the install-set: for each installed module on the tenant, return the
   published manifest; strip routes/menu/widgets the user lacks permission for; apply license
   gate (skip any whose `license.requires` is not on the tenant's plan).
3. **Host merges manifests** into a runtime registry: routes register with the Next.js / React
   Router, menu contributions feed the `Sidebar`, widgets feed `SectionRenderer`, permissions are
   added to the declared set, i18n namespaces are registered with `next-intl` / `react-i18next`.
4. **Module chunks are lazy-loaded** on first route/widget activation; SRI is verified for
   Tier-4 modules.

### Extension points (summary)

| Extension | Mechanism | Aggregator |
|-----------|-----------|------------|
| Routes | `routes[]` in manifest | Host router |
| Menu items | `menu[]` in manifest | `Sidebar` |
| Widgets | `widgets[]` in manifest, keyed by `slot` | `SectionRenderer` |
| Permissions | `permissions.declared[]` | `usePermissions()` |
| i18n | `i18n.namespace` + `i18n.path` | `next-intl` / `react-i18next` |
| Jobs / events | (not a portal concern — backend-only; see MODULE_SYSTEM.md) | — |

### Versioning

- `manifestVersion: 1` is the only accepted schema initially.
- Additive schema changes bump the patch version of the host's parser.
- Breaking schema changes require a new `manifestVersion` and a superseding ADR.

### Manifest JSON Schema

The v1 manifest schema file lives at `src/Nexora.Infrastructure/PortalExtensions/Schemas/module.manifest.v1.json` and is validated at:
1. **Build time** — CI job `validate-manifests` runs `ajv-cli` against every `module.manifest.yaml` in the repo; PR fails on schema violation.
2. **Runtime** — `ManifestLoader` validates each manifest before registration; invalid manifests are rejected with structured error log and the module is not loaded.

Schema publish location (for third-party authors): `https://schemas.nexora.io/module.manifest.v1.json` (served from the public portal).

### License gate mechanism

Manifest `license.requires` field is enforced by `ManifestAssembler`:
1. On each manifest assembly request, `ManifestAssembler` calls `IEntitlementService.HasEntitlement(tenantId, moduleId)`.
2. `IEntitlementService` is owned by Subscription module (self-hosted) or NMP Billing (SaaS) — see ADR-0023.
3. Modules without a valid entitlement are filtered out of the assembled manifest — they do not appear in navigation, menus, or widget slots.
4. Tenant-level entitlement cache: 15min TTL, invalidated by `tenant.entitlement.updated` event.

### First pilot

Phase 2 Milestone A ships either CRM or Subscription fully through this manifest mechanism.
Lessons from the pilot feed `docs/architecture/portal-extensions.md` (detail doc derived from
this ADR, written by Phase 2 Agent E).

## Consequences

### Positive
- Modules are decoupled from the host at build time; per-tenant install becomes trivial.
- Tier 3 Editions and Tier 4 Extensions are enabled by the same mechanism as first-party modules.
- License gating, permission filtering, and i18n registration converge on one file per module.
- Lazy-loading improves initial bundle size; uninstalled modules contribute zero JS.
- Review and audit of a module's contributed surface area become a schema check.

### Negative
- Added indirection: a route change now touches a manifest, not just source — authors must
  learn the schema.
- Runtime loading introduces a failure mode (bad manifest breaks menu); the host must degrade
  gracefully and surface a diagnostic.
- SRI and signature verification for Tier-4 marketplace modules require build-pipeline work;
  scoped to Phase 4.
- Testing must cover manifest-level contracts (integration test per module that round-trips its
  manifest through the assembler).

### Neutral
- Backend module contract (`IModule`) is unchanged; this ADR governs only the frontend/portal
  extension surface.
- Existing Phase 1 Portal Framework code already has stubs for route/menu/widget registration;
  this ADR formalizes and extends them.

## Implementation notes

- Manifest schema lives as a JSON Schema file in `docs/standards/schemas/module.manifest.v1.json`
  (Phase 2 Agent B creates this alongside `standards/README.md`).
- `docs/architecture/portal-extensions.md` is the long-form doc, derived from this ADR by Agent E.
- The backend assembly endpoint and host loader are engineering work for Phase 2 Milestone A.

## References

- ADR-015 — Roadmap Structure
- ADR-016 — Module Tier Classification (`tier` field in manifest)
- `docs/architecture/MODULE_SYSTEM.md` — backend `IModule` contract (unchanged)
- Phase 1 Portal Framework implementation (`src/Clients/nexora-portal`)
