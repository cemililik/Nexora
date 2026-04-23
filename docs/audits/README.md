# Audits

This directory will host periodic audit reports: security reviews, performance assessments,
compliance (GDPR, SOC 2) checks, and accessibility audits.

## Conventions

- Each audit is a single markdown file.
- **File name format**: `YYYY-MM-DD-{scope}-audit.md`
  - Examples: `2026-Q2-01-security-audit.md`, `2026-05-15-performance-audit.md`,
    `2026-06-01-gdpr-audit.md`.
- The first section of every audit is a one-paragraph executive summary followed by a
  findings table (Severity / Area / Finding / Recommendation / Status).
- Findings that result in code or process changes must link to the tracking issue and, where
  applicable, the resulting ADR.

## Methodology

Follow the standards:

- `../standards/security-review.md` — security review methodology and checklist.
- `../standards/code-review.md` — code review methodology (applies to audit scope selection).
- `../standards/OBSERVABILITY_STANDARDS.md` — what telemetry should be inspected during audits.

## Status

No audits have been recorded yet. The first scheduled audit is the Phase 2 security review
(planned at the end of the enterprise-modules milestone).

## Related

- ADR-008 — GDPR Deletion Strategy (frequently audited).
- ADR-014 — Distributed Consistency Patterns (frequently audited for data integrity).
