# Nexora Standards

Standards describe **how** we build Nexora — the concrete rules, conventions, and patterns every
contributor must follow. They are the mechanical counterpart to Architecture Decision Records
(ADRs), which describe **why** we made a particular architectural choice.

## Standards vs ADRs

| Aspect | Standards (this folder) | ADRs (`../decisions/`) |
|--------|------------------------|------------------------|
| Question | **How** do we do X? | **Why** did we choose X? |
| Content | Concrete rules, code patterns, naming conventions, checklists | Decision context, options considered, consequences |
| Lifecycle | Living — updated as practices evolve | Immutable once `Accepted` — superseded by a new ADR |
| Change gate | Requires an ADR to justify the change | Requires a new ADR (supersession) |

## Changing a Standard

A standard never changes in isolation. To modify any rule in this folder:

1. Open an ADR in `../decisions/` describing the proposed change and rationale.
2. Get the ADR to `Accepted` status.
3. Update the standard(s) and link back to the ADR in the **Derives from** section.

Standards without an ADR reference are grandfathered from the initial Nexora rule set
(see CLAUDE.md) and require an ADR only when modified.

## Index

| # | File | Summary |
|---|------|---------|
| 1 | [architectural-principles.md](architectural-principles.md) | SOLID, Modular Monolith, Clean Architecture, Result pattern, module boundaries, observability, infrastructure primitives |
| 2 | [code-style.md](code-style.md) | C# and TypeScript naming, file conventions, API endpoint convention, ApiEnvelope contract |
| 3 | [testing.md](testing.md) | Three test tiers, naming, architecture tests, mock policy, current counts |
| 4 | [code-review.md](code-review.md) | 10-category review checklist, severity, zero-tolerance rules, artifact format |
| 5 | [security-review.md](security-review.md) | OWASP lens, multi-tenant isolation, secrets, CVEs, PII handling |
| 6 | [commit-style.md](commit-style.md) | Conventional Commits, trailers, branch flow, squash merge policy |
| 7 | [localization.md](localization.md) | `lockey_` key convention, backend-returns-keys rule, 2-tier locale resolution |
| 8 | [permissions.md](permissions.md) | Permission naming, seeding, platform vs tenant scope, matrix template |
| 9 | [audit-coverage.md](audit-coverage.md) | Per-module operation-class audit matrix (MUST/SHOULD/MAY) |
| 10 | [multi-currency.md](multi-currency.md) | Money value object, exchange rates, cross-module usage, display rules |
| 11 | [documentation-style.md](documentation-style.md) | Mermaid mandate, ADR immutability, module-spec required diagrams, linking |
| 12 | [ux-ui.md](ux-ui.md) | Tab layout mandate, shared component inventory, empty/loading states, accessibility |
| 13 | [schema-migration.md](schema-migration.md) | Migration-free schema evolution: `ApplySchemaUpdatesAsync` pattern, idempotency rules, type mapping |

> **Note:** The UPPERCASE files in this directory (e.g. `CODING_STANDARDS.md`) are the legacy
> layout and remain in place until a maintainer retires them. New contributors should treat the
> lowercase files above as canonical.

## Related ADRs

- [ADR-015 Roadmap Structure](../decisions/0015-roadmap-structure.md)
- [ADR-016 Module Tier Classification](../decisions/ADR-016-module-tier-classification.md)
- [ADR-014 Distributed Consistency Patterns](../decisions/ADR-014-distributed-consistency-patterns.md)
