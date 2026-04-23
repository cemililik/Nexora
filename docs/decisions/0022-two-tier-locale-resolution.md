# 0022 — Two-tier locale resolution (platform + tenant override)

## Status

Accepted

## Date

2026-04-22

## Context

Nexora's localization contract (see `standards/localization.md` and the legacy
`LOCALIZATION_STANDARDS.md`) mandates that every user-facing string flows through the
`lockey_{scope}_{context}_{descriptor}` naming convention and that the backend never
returns translated text — only keys. Phase 1.5.3 shipped `DatabaseLocalizationService`,
which already resolves those keys against two distinct sources: a platform-shipped
catalog and a per-tenant override catalog. The behaviour is live in production
(legacy ROADMAP §1.5.3 and §1.16), but the resolution contract has never been pinned
down in an ADR, so downstream modules have no authoritative reference when they ship
new keys, tenant overrides, or cache-invalidation hooks.

Agent B (Prompt 2) flagged the gap: without a decision record, tier boundaries
(ADR-0016), cross-instance cache invalidation rules (ADR-0013), and the
"zero hardcoded strings" mandate risk drifting as new modules come online. This ADR
formalizes the already-shipped two-tier model and the invariants around it.

## Decision drivers

- Tenants must be able to rebrand or legally rephrase any user-facing string without a
  platform redeploy.
- The backend contract must remain "keys only"; translated text lives strictly on the
  resolution path.
- Cache reads must stay hot (multiple lookups per request) while staying coherent
  across horizontally scaled instances.
- Module boundaries from ADR-0016 must extend to the localization namespace — a module
  may only own keys under its own prefix.
- `en` and `tr` parity must be mechanically verifiable; missing translations must be
  observable, not silent.

## Considered options

1. **Option A — DB-backed two-tier resolution with cache + event-driven invalidation (chosen)**
   - Summary: `LocalizationResource` in the public schema carries the platform
     baseline; `LocalizationOverride` (scoped by `tenant_id`) carries tenant-specific
     values. `DatabaseLocalizationService` merges them at read time with Tier B
     (override) winning, backed by L1 (in-memory, 5 min) and L2 (Redis, 30 min)
     caches, invalidated via a `localization.key.updated` event.
   - Pros: matches the shipped Phase 1.5.3 implementation; enables per-tenant
     overrides without redeploy; cache keeps reads fast; event bus already exists per
     ADR-0013.
   - Cons: writes must publish invalidation events (one more cross-instance contract);
     tenant override writes can silently blank MUST-NOT-BE-EMPTY strings unless
     validated.

2. **Option B — Static JSON files per module, no tenant override**
   - Summary: ship translations with the binary; tenants get what the module author
     wrote.
   - Pros: simplest possible implementation; no cache; no event bus coupling.
   - Cons: tenants cannot rebrand, relabel, or legalize strings without a platform
     redeploy — this is an explicit product requirement. Rejected.

3. **Option C — External i18n SaaS (Phrase, Lokalise, etc.)**
   - Summary: delegate storage and tooling to a third-party translation service.
   - Pros: mature editor UX for translators; CDN-backed reads.
   - Cons: offline and on-prem deployments (already a stated deployment mode) cannot
     depend on a SaaS backend; storage at platform scale is cheap enough that the
     SaaS premium is unjustified. Rejected for now.

## Decision outcome

**Chosen: Option A.** The two-tier resolution model already shipped in Phase 1.5.3 is
adopted as the canonical contract:

1. All user-facing strings are stored as `lockey_{scope}_{context}_{descriptor}` keys —
   never translated text, on the wire or in DB rows that reference them.
2. Resolution proceeds in two tiers:
   - **Tier A — platform baseline:**
     `LocalizationResource(language_code, key, value, module)` in the public schema.
     Shipped with the platform; updated via EF migrations or the localization admin UI.
   - **Tier B — tenant override:**
     `LocalizationOverride(tenant_id, language_code, key, value)` in the public schema.
     Takes precedence over Tier A whenever both exist for a given
     `(tenant_id, language_code, key)` triple.
3. `DatabaseLocalizationService` merges the two tiers at read time with Tier B winning;
   both queries are cache-backed: L1 in-memory (5 min TTL) and L2 Redis (30 min TTL).
4. **Cache invalidation:** any write to `LocalizationResource` or
   `LocalizationOverride` publishes a `localization.key.updated` event; all instances
   subscribe and evict the matching prefix in both L1 and L2.
5. **Missing keys:** if neither tier has a translation, the service returns the key
   itself (e.g. `lockey_something`) and logs a `Warning`. The frontend surfaces the
   raw key in dev mode and falls back to `en` in production.
6. **Language selection:**
   - Admin UI (`nexora-admin`): browser preference → user profile setting → tenant
     default → `en`.
   - Public portal (`nexora-portal`): URL locale segment (next-intl) → cookie →
     browser → tenant default → `en`.
7. **Key namespace per module:** every module owns the prefix `lockey_{moduleName}_*`;
   cross-module keys (framework errors) live under the `framework` prefix. An
   architecture test enforces that a module may only write keys under its own prefix
   or `framework`.
8. **Language set policy:** `en` and `tr` are MUST for every module. Additional
   languages are opt-in and tracked per tenant.

This option is chosen because it matches the already-live Phase 1.5.3 code, satisfies
the tenant-rebrand requirement that rules out Option B, and keeps storage inside
platform infrastructure as required by on-prem deployments that rule out Option C.

## Consequences

### Positive

- Tenants can override any string (legal wording, industry-specific terminology,
  branding) without a platform redeploy.
- Cache keeps reads fast across hot request paths.
- Event-driven invalidation keeps multi-instance deployments coherent without
  polling.
- Module-scoped key prefixes keep ADR-0016 tier boundaries intact for translation
  ownership.

### Negative

- Writes must publish the `localization.key.updated` invalidation event — one more
  cross-instance contract to maintain (already present per ADR-0013, but now formally
  relied on here).
- Tenant overrides must respect MUST-NOT-BE-EMPTY constraints; otherwise an operator
  can silently blank critical UI strings. The localization admin UI must validate
  this on save.
- Two storage locations (resource + override) mean two migration paths when key
  schemas evolve.

### Neutral

- Existing Phase-1 modules already publish their `lockey_` catalog at migration time;
  this ADR formalizes rather than introduces that behaviour.
- Translators gain an explicit override surface but must be trained on Tier A vs.
  Tier B semantics.

## Implementation notes

- `LocalizationResource` and `LocalizationOverride` live in `Nexora.Infrastructure.Localization` (see Amendment 1).
- `localization.key.updated` event schema:
  `{ tenantId?, languageCode?, keyPrefix?, invalidatesAll }`. `tenantId == null` means
  a platform-level change and causes invalidation for all tenants.
  Kafka topic: `nexora.localization.key-updated`
- Architecture test: forbid hardcoded user-facing strings in backend responses;
  `FluentValidation.WithMessage` must match `^lockey_` regex.
- `en` + `tr` parity check in CI: a single script scans per-module
  `locales/{en,tr}/*.json`; mismatched keys fail the build.
- Observability: missing-key warnings are tagged with `{module, tenantId, key,
  languageCode}` so dashboards can track translation gaps per tenant.
- Rollout: no migration required — the two tables are already in production as of
  Phase 1.5.3. This ADR is documentation-only.

## References

- `standards/localization.md`
- `decisions/0013-cache-cross-instance-invalidation.md`
- `decisions/0016-module-tier-classification.md`
- `_archive/standards-legacy/LOCALIZATION_STANDARDS.md`
- Legacy ROADMAP §1.5.3 and §1.16 (shipped `DatabaseLocalizationService`)

---

## Amendment 1 — 2026-04-22

Resolves the maintainer TODO in Implementation notes regarding the owner project for
`LocalizationResource` and `LocalizationOverride`.

### Decision

`Nexora.Infrastructure.Localization` (folder under the existing `Nexora.Infrastructure`
project). The SharedKernel port (`ILocalizationService`) and the DB-backed implementation
(`DatabaseLocalizationService`, `LocalizationDbContext`, `LocalizationResource`,
`LocalizationOverride`) follow the same port-in-SharedKernel / adapter-in-Infrastructure
pattern already in use for `ICacheService` → `DaprCacheService`, `IJobScheduler` →
`HangfireJobScheduler`, `ISecretProvider` → `DaprSecretProvider`.

### Rationale

Localization is a cross-cutting infrastructure capability. No single module owns it; every
module contributes translation keys. Coupling it to Identity's lifecycle would mean
"uninstalling Identity uninstalls translations", which is wrong. A dedicated module would
add unnecessary ceremony (IModule, permissions, migration, manifest) for infrastructure
plumbing.

### Consequences

- No code migration needed from Phase 1.5.3 — the implementation already lives under
  Infrastructure.
- Architecture test update: forbid any non-`Infrastructure` and non-`SharedKernel` project
  from declaring types in the `Localization` folder/namespace.
- Documentation: `standards/localization.md` adds one line noting the canonical namespace.
