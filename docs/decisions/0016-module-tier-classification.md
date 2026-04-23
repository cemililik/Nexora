# ADR-016: Module Tier Classification

## Status
Accepted

## Date
2026-04-22

## Context

Phase 1 delivered 8 production-grade platform modules (Identity, Contacts, Notifications,
Documents, Audit, Reporting, Portal Framework, Admin Dashboard). The product is now pivoting
from a vertical-first (NGO/Education) story to an **enterprise/SMB core** with vertical Editions
layered on top.

Without a tier classification we hit three problems:

1. **Dependency chaos.** Nothing prevents a Tier-3 vertical module (e.g. Fundraising) from
   becoming a build-time dependency of a Tier-1 core module. Today's CRM spec already bled NGO
   vocabulary into core.
2. **Packaging.** The marketplace / NMP billing model needs to know what ships "in the box" vs.
   what's a paid edition vs. what's an optional extension. There is no agreed taxonomy.
3. **Scope creep per phase.** Without tiers, Phase 2 ("enterprise core") gets confused with
   Phase 3a ("NGO Edition"); features cross tiers arbitrarily.

We need a stable classification that governs dependency direction, packaging, and phase scope.

## Decision drivers

- Dependency rules must flow **in one direction only** (higher tiers may depend on lower tiers;
  never the reverse).
- Tier boundaries must match the packaging/billing model the business is adopting (Core / Edition /
  Extension).
- Classification must remain stable across phases even as features are added within a module.
- Vertical Editions (NGO, Education) must be swappable and independently sellable.

## Considered options

### Option A — No formal tiers; use phase order as proxy
Rely on phase sequencing alone. Modules developed in Phase 1 are "core" by accident.
- (+) No new vocabulary.
- (−) Phase order encodes time, not architectural role; regressions happen whenever a "later"
  module needs something "earlier" unidirectionally depends on it.
- (−) No packaging story.

### Option B — Two tiers: Core vs. Optional
Flat binary split. Everything non-core is "optional".
- (+) Simple.
- (−) Collapses three distinct commercial tiers (enterprise upsell, vertical edition, marketplace
  extension) into one bucket. Billing model cannot express them.

### Option C — Four tiers: Platform Core / Enterprise Core / Vertical Editions / Extensions
A tier-per-purpose model with explicit dependency rules.
- (+) Mirrors the commercial model: free-in-platform vs. paid-core vs. paid-edition vs. marketplace.
- (+) Dependency direction is unambiguous.
- (−) More vocabulary, enforced via architecture tests.

## Decision outcome

**Chosen: Option C.** Nexora modules are classified into four tiers:

### Tier 1 — Platform Core
Modules every tenant needs regardless of vertical. Ship in every deployment. No license gating.

**Current members:** Identity, Contacts, Notifications, Documents, Audit, Reporting,
Portal Framework, Admin Dashboard.

**Rules:**
- May depend on: SharedKernel, Infrastructure.
- May **not** depend on: any higher tier.
- Vocabulary must be domain-neutral (no NGO, education, retail, etc. language in specs or code).

### Tier 2 — Enterprise Core
Modules that cover generic B2B/SMB business operations. Packaged in the enterprise tier; unlock
via license (NMP).

**Planned members:** CRM (generic), Subscription & Billing, Finance, Projects, HR Core.

**Rules:**
- May depend on: SharedKernel, Infrastructure, any Tier-1 module.
- May **not** depend on: Tier-3 (verticals) or Tier-4 (extensions).
- Must publish a Portal Extension manifest (see ADR-017) to appear in the portal.

### Tier 3 — Vertical Editions
Domain-specific bundles that layer on top of Tier 1 + 2. Each edition is its own SKU.

- **Tier 3a — NGO Edition:** Fundraising (donations + sponsorship merged), NGO-flavored Events.
- **Tier 3b — Education Edition:** Student, Class, Grade, Curriculum, Attendance, Feedback,
  Evaluation, Parent Portal.

**Rules:**
- May depend on: SharedKernel, Infrastructure, any Tier-1, any Tier-2.
- May **not** depend on: other Tier-3 editions (editions are orthogonal; a tenant may install
  zero, one, or several).
- Must ship its own i18n namespace, permissions namespace, and route prefix.

### Tier 4 — Extensions
Optional modules with narrow scope; distributed through the marketplace. Not part of any tier
SKU; buy-as-you-go.

**Planned members:** POS, Fleet, Inventory, Surveys, CMS.

**Rules:**
- May depend on: SharedKernel, Infrastructure, Tier 1, Tier 2, Tier 3 (when the extension is
  edition-specific).
- Must remain optional — no Tier 1–3 module may take a hard dependency on a Tier-4 module.
- Publication model: each extension ships a signed manifest to the marketplace.

### Dependency rule (summary)

```
Tier 4  ──may-depend-on──▶  Tier 1, 2, 3
Tier 3  ──may-depend-on──▶  Tier 1, 2
Tier 2  ──may-depend-on──▶  Tier 1
Tier 1  ──may-depend-on──▶  (SharedKernel, Infrastructure only)
```

Enforced by architecture tests in `tests/Nexora.Architecture.Tests/` — one test per tier
boundary. Violations fail the build.

### Packaging / commercial model (informative)

| Tier | SKU | License gate |
|------|-----|--------------|
| Tier 1 | Included in every deployment | none |
| Tier 2 | Enterprise plan | NMP subscription |
| Tier 3a | NGO Edition | NMP subscription add-on |
| Tier 3b | Education Edition | NMP subscription add-on |
| Tier 4 | Marketplace | per-extension license |

The commercial model is informative here; contractual terms are owned by the NMP track.

## Consequences

### Positive
- Dependency direction is explicit and mechanically enforced.
- Phase scope becomes unambiguous (Phase 2 = Tier 2; Phase 3a = Tier 3a NGO; etc.).
- Packaging/billing integration has a clear taxonomy to plug into.
- Vertical Editions are independently sellable and swappable.
- New contributors can place a proposed module in one tier in ≤ 1 minute.

### Negative
- Existing specs that leaked vertical vocabulary into Tier 1 (contacts, CRM) must be cleaned
  (scheduled for Prompt 02).
- Architecture tests must be added per boundary; false positives possible during early churn.
- The "generic Event" vs. "NGO Event" split may need a future Tier-2 event module if enterprise
  customers want events without NGO framing.

### Neutral
- Module folder layout changes (`docs/modules/tier-N-xxx/…`), but code namespaces are unchanged.
- Permissions and localization namespaces remain module-scoped; tier is a packaging concept,
  not a runtime concept.

## Implementation notes

- Tier is declared in each module's spec header (`**Tier:** 2 — Enterprise Core`).
- Tier is also declared in each module's runtime manifest (see ADR-017) for marketplace /
  license-gate tooling.
- Architecture tests live in `tests/Nexora.Architecture.Tests/TierBoundaryTests.cs` (added in
  Phase 2 Agent D as part of module reorganization follow-up — not in Phase 1 scope).

## References

- ADR-015 — Roadmap Structure (phase model uses tier classification)
- ADR-017 — Portal Extension Architecture (manifest schema; tier field)
- `docs/_archive/migration-notes.md` §1.3 — tier mapping for every current module
