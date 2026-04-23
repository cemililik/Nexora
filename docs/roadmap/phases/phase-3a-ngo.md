# Phase 3a — NGO Edition

**Tier:** 3a — NGO Vertical Edition
**Status:** Not started
**Planned dates:** follows Phase 2; HR Core not a hard prerequisite

NGO Edition packages fundraising, sponsorship, and NGO-flavored events into an
independently sellable SKU on top of Tier 1 + Tier 2 per ADR-016. Donations and
Sponsorship modules merge into a single `fundraising` module; events get split into a
generic Tier-2 future module and an NGO-flavored `events-ngo` living here.

---

## Exit bar

1. `fundraising` module (merged donations + sponsorship) in production under
   `modules/tier-3a-ngo/fundraising/`.
2. `events-ngo` module (NGO-flavored events) shipped under
   `modules/tier-3a-ngo/events-ngo/`.
3. Tax receipt templates (US + Turkish bağış makbuzu) live — the Phase 1.5.3 deferred
   item is closed here.
4. Donor portal + public donation pages live via ADR-017 manifest.
5. Architecture test confirms Tier-3a modules do **not** depend on Tier-3b modules
   (editions are orthogonal per ADR-016).

## Scope

### 3a.1 Fundraising (donations + sponsorship merged)

- Donation categories (cause, campaign, program).
- One-time + recurring donations.
- Donation cart flow.
- Multiple payment gateways (Stripe, iyzico, Param).
- Multi-currency donations.
- Automatic receipts (US + TR templates).
- Donor matching (duplicate detection against Contacts).
- Guest donations (no account required).
- Bank transfer import + reconciliation.
- Video / media support on campaign pages.
- Zakat calculator (Muslim-audience tenants).
- Landing page builder for campaigns.
- Campaigns + crowdfunding (goal, thermometer, milestones).
- Physical collection points (POS-lite for in-person).
- Reports + finance integration (auto-record into Finance).
- Donor portal.
- Sponsorship program management (sponsor-beneficiary matching, installment plans,
  payment reminders, progress updates, aid distribution, reports, sponsor portal).

### 3a.2 Events (NGO-flavored)

- Event creation (generic event entity; NGO framing via category/template).
- Categories + templates.
- Registration + ticketing.
- QR check-in.
- Venue + speaker management.
- Sponsor history.
- Event calendar + external calendar sync.
- Event reports.
- Public event pages.

## Out of scope

- Generic (non-NGO) events module — may split into Tier-2 later if enterprise customers
  demand it; not in scope here.
- Education-edition flows — Phase 3b.
- Tier-4 extensions (POS, Fleet, etc.).

## Milestones

### Milestone A — Fundraising core
Merged donations + sponsorship module, tax receipts, Stripe + iyzico gateways, guest
donations, donor portal skeleton.

### Milestone B — Campaigns + events
Campaign/crowdfunding surfaces, landing page builder, Zakat calculator, NGO-flavored
events module.

### Milestone C — Integrations + polish
Finance auto-record, bank import reconciliation, Param gateway, advanced sponsor flows,
WCAG pass on donor + sponsor portals.

## Acceptance criteria

- [ ] Merged `fundraising` spec replaces the separate `donations` + `sponsorship` specs
      (Prompt 02 content task; Phase 2 Agent D moves the file skeletons).
- [ ] `events-ngo` spec exists under the Tier-3a folder.
- [ ] Receipt PDFs generated for US + TR sample donations with locale-aware formatting.
- [ ] Tier-3a modules pass the architecture test (no Tier-3b imports).
- [ ] Donor portal surfaces via ADR-017 manifest.

## ADR ledger

**Introduces:** TBD (receipt template engine and Zakat calculator may warrant ADRs).

**Consumes:** ADR-015, ADR-016, ADR-017, ADR-008 (GDPR for donor data), ADR-010
(notification delivery) + Phase-1 foundations.

**Supersedes:** the separate donations + sponsorship specs (merged into `fundraising`).

## Informs

- `phase-4-extensions.md` — POS at collection points may integrate with fundraising.
- NMP track — NGO Edition is a priced add-on.

## References

- Legacy source: `docs/roadmap/ROADMAP.md` §4.4 + §4.5 + §3.2.
- Migration plan: `../../_archive/migration-notes.md` §2.4.
- Deferred from Phase 1.5: tax-receipt templates (now in scope here).
