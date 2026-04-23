---
name: supersede-adr
description: Supersede an existing Accepted ADR by writing a new ADR that replaces it and flipping the old one to "Superseded by ADR-XXX". Use when the user says "supersede ADR-NNN", "replace this decision", or proposes a change that contradicts an Accepted ADR.
---

# Supersede ADR

ADRs are immutable once Accepted. To change a decision, write a **new ADR** that
explicitly supersedes the old one; the old ADR's Status flips to `Superseded by ADR-XXX`
but its body stays intact as historical record.

## When to use

- An Accepted ADR no longer matches reality (tech pivot, tier change, security finding).
- A business review's Adjuster section proposes a scope change that contradicts an ADR.
- Legal / compliance forces a change that a prior ADR prohibited.

## When NOT to use

- ADR is still `Proposed` → edit it in place, don't supersede.
- Minor clarification → append an amendment section (see ADR-005, ADR-011 precedent).
- Change doesn't contradict the ADR → write a new companion ADR, don't supersede.

## Inputs

- Target ADR number (the one being superseded).
- Reason for superseding (2–3 sentences).
- New decision (what replaces it).

## Procedure

1. **Read the target ADR.** Understand exactly what it said. Identify every downstream
   doc that cites it (phase files, standards, module specs).
2. **Write the new ADR** (use the `write-adr` skill).
   - Status: `Proposed`.
   - In the Context section, cite the superseded ADR by number.
   - Add a dedicated section **`## Supersedes`** listing `ADR-NNN — <title>` and the
     reason.
   - Reproduce enough of the old ADR's context that readers do not have to chase links
     to understand the change.
3. **Flip the old ADR's Status.** Edit `docs/decisions/ADR-NNN-*.md`:
   - Change Status line to `Superseded by ADR-XXX (YYYY-MM-DD)`.
   - Do **not** modify the rest of the file.
4. **Update cross-links.**
   - Phase-file ADR ledgers: move from `Consumes` to `Supersedes` (in the new ADR's
     owning phase) and/or update consumer phases to point at the new ADR.
   - `docs/roadmap/current.md` pending-decisions table, if relevant.
5. **Do not promote.** Leave the new ADR `Proposed`; only the maintainer promotes to
   `Accepted` and closes the supersession.
6. **Open follow-up tasks.** Any code or docs that relied on the old ADR's rules needs
   a task (skill: `start-task`).

## Immutability rules

- Never delete the old ADR.
- Never rewrite the old ADR's Context / Decision / Consequences sections.
- The only edit permitted on a superseded ADR is the Status line.

## Outputs

- New ADR file in `Proposed` status with a `## Supersedes` section.
- Old ADR file with Status flipped to `Superseded by ADR-XXX`.
- Updated phase-file ADR ledgers and (if applicable) `current.md`.
- Open follow-up tasks for downstream cleanup.

## Related standards

- `docs/standards/DOCUMENTATION_STANDARDS.md` (ADR immutability rule)
- [ADR-015 — Roadmap Structure](../../../docs/decisions/ADR-015-roadmap-structure.md)
  (status vocabulary; `Superseded` is maintainer-set)
- Sibling skills: `write-adr`, `conduct-business-review`
