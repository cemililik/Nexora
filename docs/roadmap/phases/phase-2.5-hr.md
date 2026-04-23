# Phase 2.5 — HR Core

**Tier:** 2 — Enterprise Core (HR split)
**Status:** Not started
**Planned dates:** follows Phase 2; exact window TBD

HR Core is separated from the main Phase 2 bundle because payroll cycles, leave policies,
and third-party HRIS sync have a different release cadence than CRM/Finance/Projects. It
remains strictly Tier-2 per ADR-016.

---

## Exit bar

1. Employee records, department hierarchy, and contract management shipped.
2. Payroll runs, leave management, attendance, and shift scheduling shipped.
3. Self-service portal (leave requests, payslips, documents) live via ADR-017 manifest.
4. At least one HRIS connector (Gusto, ADP, or BambooHR) in production.
5. Tier-boundary tests green (no Tier-3/4 imports).

## Scope

- Employee records (core profile, employment history).
- Departments + organizational chart.
- Contracts (fixed-term, indefinite, probation).
- Payroll runs (gross → net, deductions, taxes — per locale).
- Leave management (policies, balances, approvals).
- Attendance tracking.
- Shift scheduling.
- Personnel documents (contracts, IDs — via Documents module).
- Self-service portal for employees.
- HRIS sync (Gusto / ADP / BambooHR — pick one first).

## Out of scope

- Recruitment / ATS — candidate for future phase or Tier-4 extension.
- Performance reviews — future phase.
- Learning management — future phase.
- NGO/Education-specific staff flows — Tier-3 editions handle vertical-specific needs.

## Milestones

### Milestone A — Core records
Employees, departments, contracts, documents integration.

### Milestone B — Time & payroll
Attendance, shifts, leave, payroll runs.

### Milestone C — Portal + HRIS sync
Self-service portal (ADR-017 manifest), first HRIS connector, locale parity.

## Acceptance criteria

- [ ] Employee → User link is optional and permission-gated.
- [ ] Payroll run is idempotent and produces a signed audit trail.
- [ ] Leave balances recompute correctly across year boundaries.
- [ ] Self-service portal passes WCAG 2.1 AA.
- [ ] HRIS connector round-trips a sample employee without data loss.

## ADR ledger

**Introduces:** TBD (payroll calculation strategy may warrant an ADR).

**Consumes:** ADR-015, ADR-016, ADR-017 + Phase-1 foundations.

**Supersedes:** none.

## Informs

- `phase-3b-education.md` — Education staff management may extend the HR Core schema.
- NMP track — HR license tier flows into NMP subscription add-ons.

## References

- Legacy source: `docs/roadmap/ROADMAP.md` §3.4.
- Migration plan: `../../_archive/migration-notes.md` §2.3.
