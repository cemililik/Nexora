---
name: write-adr
description: Draft a new ADR in docs/decisions/ using the Nexora ADR format (Proposed by default; maintainer promotes to Accepted). Use when the user says "write an ADR", "record this decision", or introduces a cross-cutting architectural choice. Prefer this skill over the legacy create-adr for new ADRs following the restructure.
---

# Write ADR

Architecture Decision Records are the **why** behind Nexora's architecture. They are
immutable once `Accepted`. Superseding is itself a new ADR (see the `supersede-adr`
skill).

## When to use

- Any decision that crosses module boundaries, changes a tier, or alters the packaging
  model.
- Any change to the roadmap structure, status vocabulary, or tier classification.
- Any externally-visible contract: API envelope, ApiEnvelope<T> shape, permission
  namespace, integration-event schema.

## When NOT to use

- Purely tactical code changes confined to one module → do a PR + task instead.
- Documentation rewrites → edit the doc; no ADR needed unless policy changes.

## Inputs

- Problem statement (what's broken / missing).
- Decision drivers (what constraints matter).
- At least two alternatives considered (Option A / B / …).
- The chosen option.

## Procedure

1. **Allocate the next ADR number.** List `docs/decisions/ADR-*.md` — use the next
   integer. After the Phase 2 restructure, new ADRs follow `NNNN-kebab-title.md` (four
   digits); continue whichever convention the repo is on at write time.
2. **Set Status = `Proposed`.** Only the maintainer promotes to `Accepted`.
3. **Use the Nexora ADR structure:**
   - Title: `ADR-NNN: <Title>` (or `NNNN-kebab-title.md`).
   - `## Status` — Proposed | Accepted | Superseded by ADR-XXX | Deprecated.
   - `## Date` — today.
   - `## Context` — factual, no conclusions.
   - `## Decision drivers` — bulleted constraints.
   - `## Considered options` — one subsection per option, with (+) pros and (−) cons.
   - `## Decision outcome` — **Chosen: Option X.** Followed by normative rules.
   - `## Consequences` — Positive / Negative / Neutral.
   - `## Implementation notes` — optional; how to roll out.
   - `## References` — links to related ADRs, standards, migration notes.
4. **Cross-link.** Add the ADR to the owning phase file's ADR ledger (Introduces /
   Consumes / Supersedes).
5. **Sync-point.** If the ADR changes the active phase or next-phase entry criteria,
   update `docs/roadmap/current.md` in the same commit (ADR-015 rule).
6. **Do not promote.** Leave Status = `Proposed`; the maintainer owns promotion.

## Nexora-specific rules

- Use Mermaid for any diagrams, embedded inline (DOCUMENTATION_STANDARDS.md).
- Respect tier rules (ADR-016): decisions that bind a higher tier to depend on a lower
  tier are fine; the reverse needs explicit justification.
- `lockey_` key naming, ApiEnvelope<T> contracts, and schema-per-tenant are assumed —
  do not re-litigate them; reference the ADR that set them.

## Outputs

- `docs/decisions/ADR-NNN-<slug>.md` (or `NNNN-<slug>.md`) in Proposed status.
- Updated phase file ADR ledger.
- (Optional) updated `current.md` if the sync-point rule applies.

## Related standards

- `docs/standards/DOCUMENTATION_STANDARDS.md`
- [ADR-015 — Roadmap Structure](../../../docs/decisions/ADR-015-roadmap-structure.md)
- Legacy `create-adr` skill — kept for backward compatibility; prefer `write-adr` for
  the restructure era.
