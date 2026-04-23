# Phase 4 — Extensions

**Tier:** 4 — Marketplace Extensions
**Status:** Not started
**Planned dates:** post Phase 3; continuous thereafter

Tier-4 extensions are optional, buy-as-you-go modules distributed through the
marketplace (ADR-016). No Tier 1–3 module may take a hard dependency on a Tier-4 module.
This phase also hosts the cross-phase continuous workstreams (performance, security,
accessibility, docs, additional locales, connectors) that have no natural home in a
numbered phase.

---

## Exit bar

Phase 4 is a rolling phase — it does not "close" in the same sense as earlier phases.
Individual extensions hit their own exit bars:

1. Extension ships a signed marketplace manifest (ADR-017 + ADR-016 Tier-4 rules).
2. Extension passes the Tier-4 architecture test (no upstream Tier 1–3 module imports
   Tier-4 namespaces).
3. Extension surfaces via ADR-017 manifest in the portal and/or admin.
4. Locale parity (en + tr minimum) and WCAG 2.1 AA pass.

## Scope

### 4.1 Accounting (advanced)

Advanced accounting on top of Phase 2 Finance — Chart of Accounts, double-entry, fiscal
years, bank reconciliation, expense approval, budget variance, multi-currency, tax rates,
consolidated reports, auto-journaling, QuickBooks/Xero advanced flows.

### 4.2 POS

Sales screen, sessions, catalog, payments, receipts, cash management, end-of-day
reconciliation, event POS, offline mode, inventory integration, accounting integration,
Square sync.

### 4.3 Fleet

Vehicles, assignment, insurance, maintenance scheduling + records, fuel logging,
inspections, documents, cost tracking, dashboards.

### 4.4 CMS

Multi-site, page builder, blog, SEO, form builder, themes, media library, navigation,
redirects, multi-language, mobile-responsive output, live chat, self-service.

### 4.5 Surveys

Builder, sections/branching, distribution, anonymity, real-time, analytics, templates,
recurring, export.

### 4.6 Inventory

Warehouses, locations, catalog, movements, assets, assignment, stocktake, alerts,
suppliers, reports.

### 4.7 Cross-phase continuous

These run alongside every phase but have no dedicated home:

- Performance / load testing.
- Security audits + pen testing.
- WCAG 2.1 AA accessibility.
- User + API + developer documentation.
- Automated testing expansion.
- Portal UI per-vertical releases.
- Additional locales beyond en/tr.
- 3rd-party connectors.
- Remove Infrastructure dependency from Contacts unit tests (Phase 1 tech debt).
- Mobile app (React Native).
- Marketplace (3rd-party modules).

## Out of scope

- Anything a Tier 1–3 module could reasonably depend on — that belongs in an earlier
  tier by ADR-016.
- NMP billing and license enforcement — parallel NMP track.

## Milestones

Tier-4 is released extension-by-extension, not as one shippable milestone bundle. The
maintainer sequences extensions by market demand. Cross-phase continuous items (§4.7)
are treated as standing work, not milestones.

- **Milestone A — Finance-adjacent:** Accounting advanced, POS, Inventory. These share
  the Finance auto-record surface from Phase 2.
- **Milestone B — Ops:** Fleet, Surveys. Independent enough to ship in any order.
- **Milestone C — Web:** CMS. Coordinate with Portal Framework evolution.

## Acceptance criteria (per extension)

- [ ] Signed marketplace manifest (ADR-017).
- [ ] Tier-4 architecture test green.
- [ ] No upstream Tier 1–3 module imports this extension.
- [ ] Locale parity (en + tr).
- [ ] WCAG 2.1 AA pass on any portal surface.
- [ ] Marketplace listing assets (screenshots, description) complete.

## ADR ledger

**Introduces:** TBD per extension.

**Consumes:** ADR-015, ADR-016 (Tier-4 rules), ADR-017 (manifest + signing).

**Supersedes:** none.

## Informs

- NMP track — marketplace revenue share and license enforcement flow.
- Future tiering decisions — if enterprise customers demand an extension in-the-box, it
  may be promoted to Tier 2 via an ADR.

## References

- Legacy source: `docs/roadmap/ROADMAP.md` §4.1–§4.3, §3.1, §3.3, §3.5, and the
  cross-phase continuous list.
- Migration plan: `../../_archive/migration-notes.md` §2.6, §2.7.
