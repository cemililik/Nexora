# Business Review — Master Plan

A business review is not a single author's opinion. It is a **merge** of five role-based
readings of the roadmap, each produced independently and then reconciled. This file
defines the five roles, their deliverables, and the merge step.

---

## Why five roles

A one-author review conflates history, deltas, lessons, corrections, and forward plans.
Readers cannot tell which claim is backed by evidence vs. which is a recommendation.
Splitting the review into five independent passes — each with a narrow remit — keeps
each claim traceable to the role that owns it.

## The five roles

### 1. Chronicler — "What actually happened?"

Produces an append-only narrative of the last period (since the previous business
review). Sources: merged PRs, ADR promotions, task status transitions, `current.md`
history.

- **Tone:** factual, past tense, no recommendations.
- **Output:** `chronicler.md` section of the review file.
- **Links:** every factual claim cites a PR, task ID, or ADR.

### 2. Plan-diff — "What did the plan say vs. what shipped?"

Compares the previous period's phase file snapshot (or `current.md`) against what the
Chronicler reports.

- **Tone:** variance report; +/- per milestone.
- **Output:** `plan-diff.md` section.
- **Calls out:** scope additions without ADR; silently deferred items; milestones met
  but with caveats.

### 3. Learning — "What did we learn?"

Extracts reusable lessons: what assumptions broke, what patterns worked, what to
document as standards or ADRs.

- **Tone:** reflective; each lesson has a claim + evidence + proposed follow-up.
- **Output:** `learning.md` section.
- **Triggers:** may propose new entries for `docs/standards/` or a new ADR.

### 4. Adjuster — "What must change in the plan?"

Proposes concrete edits to phase files, task priorities, or `current.md` based on
Chronicler facts and Learning lessons.

- **Tone:** directive; each proposal is a specific diff.
- **Output:** `adjuster.md` section.
- **Rule:** scope changes must route through a new ADR (ADR-015); Adjuster drafts it,
  maintainer promotes.

### 5. Pathfinder — "What's next beyond the current horizon?"

Looks one phase past the currently active and next phase. Not a plan; a radar.

- **Tone:** speculative, tagged `radar:` — not actionable without further review.
- **Output:** `pathfinder.md` section.
- **Does not:** edit phase files directly.

## The merge step

After all five passes are filed, the merge step combines them into one
`YYYY-MM-DD-<slug>.md` business review file with these sections in order:

1. **Context** — which period, which active phase, which next phase.
2. **Chronicler narrative** (immutable once filed).
3. **Plan-diff table** (Milestone → planned vs. actual vs. variance note).
4. **Learning list** (claim → evidence → follow-up).
5. **Adjuster proposals** (each with: target file, diff summary, ADR required? Y/N).
6. **Pathfinder radar** (tagged items, no action required).
7. **Maintainer sign-off block** (date, approver, ADRs promoted, follow-up tasks opened).

The merged file is the **review of record**. Source passes may be kept as an appendix
or discarded once merged; the merge is authoritative.

## Who runs each role

- Any contributor may author any pass, but no single contributor may author two passes
  for the same review (preserves independence).
- The maintainer runs or blesses the merge step and signs off.

## Relationship to other docs

- A business review **consumes** phase files, tasks, ADRs, and the four sibling review
  kinds (code / security / performance / this).
- A business review **may produce**: new ADRs, phase-file edits, task re-prioritization,
  `current.md` updates. It does not silently alter them — every change is listed in the
  Adjuster section and gated by maintainer sign-off.

## Template outline (for concrete reviews)

```markdown
# Business Review — <period / trigger>

## Context
## 1. Chronicler narrative
## 2. Plan-diff
## 3. Learning
## 4. Adjuster proposals
## 5. Pathfinder radar
## Maintainer sign-off
```

## Related

- [Business reviews README](README.md)
- [ADR-015 — Roadmap Structure](../../../decisions/0015-roadmap-structure.md)
- [Roadmap current](../../../roadmap/current.md)
