# Phase 3b — Education Edition

**Tier:** 3b — Education Vertical Edition
**Status:** Not started
**Planned dates:** follows Phase 2; independent of Phase 3a

Education Edition is an orthogonal vertical to NGO (per ADR-016: editions do not depend
on each other). It layers student/class/grade/curriculum domain on top of Tier 1 + Tier 2.

---

## Exit bar

1. `education` module in production under `modules/tier-3b-education/education/`.
2. Parent portal live via ADR-017 manifest.
3. Academic calendar + enrollment pipeline (CRM template) wired end-to-end.
4. Tuition billing runs through Subscription & Billing (Phase 2).
5. Architecture test confirms Tier-3b does **not** depend on Tier-3a modules.

## Scope

- Academic year + terms.
- Grade levels + classrooms.
- Student records (core profile, guardian links).
- Enrollment pipeline (CRM template).
- Guardian linking (via Contacts relationships).
- Appointments + staff availability.
- Academic calendar.
- Accreditation tracking.
- Summer camp + seasonal programs.
- Student documents (contracts, IDs — via Documents).
- Waitlist management.
- Parent portal.

## Out of scope

- Learning Management System (LMS) — future phase or Tier-4 extension.
- Gradebook + report cards — future phase.
- NGO-specific flows — Phase 3a.

## Milestones

### Milestone A — Core records
Academic year, grade levels, classrooms, students, guardian linking.

### Milestone B — Enrollment + billing
CRM enrollment pipeline, tuition via Subscription, appointments, academic calendar.

### Milestone C — Parent portal + extras
Parent portal (ADR-017 manifest), waitlist, accreditation tracking, summer camp.

## Acceptance criteria

- [ ] `education` module spec exists under the Tier-3b folder.
- [ ] Parent portal surfaces via ADR-017 manifest.
- [ ] Enrollment pipeline reuses the CRM template pattern (no duplicate pipeline engine).
- [ ] Tuition billing uses Subscription & Billing (no custom billing engine).
- [ ] Tier-boundary architecture test passes.

## ADR ledger

**Introduces:** TBD (academic calendar vs. fiscal calendar reconciliation may warrant an
ADR).

**Consumes:** ADR-015, ADR-016, ADR-017, CRM + Subscription from Phase 2.

**Supersedes:** none.

## Informs

- NMP track — Education Edition is a priced add-on.
- Potential future LMS / Gradebook extensions (Tier-4) may depend on this module.

## References

- Legacy source: `docs/roadmap/ROADMAP.md` §4.6.
- Migration plan: `../../_archive/migration-notes.md` §2.5.
