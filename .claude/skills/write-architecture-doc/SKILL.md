---
name: write-architecture-doc
description: Write or update a doc under docs/architecture/ — cross-module architecture narrative with mandatory Mermaid diagrams per DOCUMENTATION_STANDARDS.md. Use when the user says "document the architecture of X", "write an overview for Y", or introduces a new cross-cutting mechanism (portal extensions, saga pattern, etc.).
---

# Write Architecture Doc

Produce or update a doc in `docs/architecture/`. Architecture docs explain **how the
platform fits together** — module boundaries, communication flows, runtime topology —
not a single module (that's a module spec) and not a decision rationale (that's an ADR).

## When to use

- A new cross-cutting mechanism is introduced (portal extensions, saga orchestration,
  multi-currency engine).
- An existing `docs/architecture/*.md` is outdated and needs a pass.
- Phase 2 Milestone A produces lessons to fold into `portal-extensions.md`.

## When NOT to use

- Single-module deep dive → `write-module-spec`.
- Decision rationale → `write-adr`.
- Standards / conventions → contribute to `docs/standards/`.

## Inputs

- Topic (e.g. "portal extensions", "multi-tenancy", "communication flow").
- Scope level (platform-wide, tier-wide, subsystem).
- Related ADRs that constrain this area.

## Procedure

1. **Choose the target file.** If replacing an existing overview, read it first and
   keep the diff minimal. Common homes:
   - `docs/architecture/OVERVIEW.md` — high-level platform.
   - `docs/architecture/MODULE_SYSTEM.md` — IModule contract, loader, manifest.
   - `docs/architecture/MANAGEMENT_PORTAL.md` — NMP.
   - `docs/architecture/COMMUNICATION_FLOW.md` — APISIX + Dapr + observability.
   - New: `docs/architecture/portal-extensions.md` (Phase 2 Milestone A), or others.
2. **Sections (Nexora convention):**
   - Purpose — what this doc covers and does not cover.
   - Context — constraints, related ADRs (link them).
   - Architecture — prose + **at least one Mermaid diagram**.
   - Components — responsibilities per box.
   - Flows — sequence diagrams (Mermaid) for the 2–3 most important interactions.
   - Trade-offs — what we chose and why not the alternatives (link ADRs).
   - References — ADRs, standards, module specs.
3. **Mermaid, inline only.** Renders natively in GitHub. No external images.
4. **Respect tier rules (ADR-016).** Any arrow crossing tiers must flow in the allowed
   direction (lower ← higher). Call out and justify any apparent violation.
5. **Localization-aware.** Where user-facing strings appear in examples, use
   `lockey_` keys per LOCALIZATION_STANDARDS.
6. **API examples.** Any sample response uses `ApiEnvelope<T>` per
   API_INTEGRATION_STANDARDS.
7. **Link back.** Add the new / updated doc to `docs/README.md` and (if it introduces a
   new mechanism) to the relevant phase file's References.

## Mandatory diagram types

- **Component diagram** for any platform subsystem.
- **Sequence diagram** for any runtime flow the doc describes.
- **Deployment/topology diagram** if the doc covers infra.

## Output

- One updated / created `.md` file under `docs/architecture/`.
- (Optional) updated top-level `docs/README.md` index.

## Related standards

- `docs/standards/DOCUMENTATION_STANDARDS.md`
- [ADR-016 — Module Tier Classification](../../../docs/decisions/ADR-016-module-tier-classification.md)
- `docs/architecture/MODULE_SYSTEM.md` — the IModule contract your diagrams must respect
