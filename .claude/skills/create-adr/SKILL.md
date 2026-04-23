---
name: create-adr
description: Create a new Architecture Decision Record (ADR) in docs/decisions/ following DOCUMENTATION_STANDARDS.md. Use when the user asks to "write an ADR", "document a decision", or makes a cross-cutting architecture choice that needs to be recorded.
---

# Create ADR

ADRs are **immutable once accepted**. Superseded decisions get a new ADR that references the old one.

## File location & naming
`docs/decisions/ADR-{NNN}-{kebab-case-title}.md`

Look up the next number by listing existing ADRs; don't reuse or skip numbers.

## Required sections
```markdown
# ADR-{NNN}: {Title}

- **Status**: Proposed | Accepted | Superseded by ADR-XXX | Deprecated
- **Date**: YYYY-MM-DD
- **Deciders**: {names/roles}
- **Tags**: {module, cross-cutting, security, ...}

## Context
What problem are we solving? What constraints apply? Keep it factual, no conclusions yet.

## Decision
The choice that was made, stated as an imperative.

## Alternatives Considered
- **Option A** — pros / cons / why rejected.
- **Option B** — pros / cons / why rejected.

## Consequences
- Positive: ...
- Negative / trade-offs: ...
- Neutral: ...

## References
- Related ADRs, specs, tickets.
```

## Rules
- Mermaid for any diagram, embedded inline — never separate images.
- Don't rewrite an accepted ADR; write a new one with `Supersedes ADR-XXX` and set the old one to `Superseded by ADR-{this}`.
- Link the ADR from relevant module specs / standards docs.
- Keep it under ~400 lines — if longer, link out to a spec doc.

## After creating
- Update `docs/decisions/README.md` (if an index exists) with the new entry.
- If this ADR introduces a rule affecting coding, update the relevant `docs/standards/*.md`.
