---
name: perform-code-review
description: Perform a code review on a Nexora PR or branch — checking module boundaries, Result<T> usage, lockey keys, structured logging, ApiEnvelope<T>, tab-based layouts, and permission gating — and file the findings under docs/analysis/reviews/code-reviews/. Use when the user says "review this PR", "code review", or "check changes before merge".
---

# Perform Code Review

Review Nexora changes against the mandatory standards and file a dated findings document
under `docs/analysis/reviews/code-reviews/`.

## When to use

- Any PR touching `src/Modules/**` or `src/Clients/**`.
- Any cross-module refactor (module boundaries, shared kernel additions).
- Pre-merge sanity pass for Tier-1 and Tier-2 changes.

## Inputs

- PR number or branch name.
- Scope hint (module, tier).
- Optional: specific concerns the author flagged.

## Procedure

1. **Read the PR / diff.** Identify which modules and tiers are touched. Flag any
   higher-tier → lower-tier violation (ADR-016).
2. **Check module boundaries.**
   - No direct references to another module's internal types.
   - Cross-module calls via MediatR notifications, SharedKernel interfaces, or Dapr
     pub/sub integration events only.
   - Module tables prefixed `{module}_{table}`.
3. **Check domain + handler patterns.**
   - Result<T> for expected failures; exceptions only for unexpected.
   - `DomainException` only thrown from domain entities; handlers return
     `Result.Failure(LocalizedMessage.Of("lockey_..."))`.
   - Command validators present (FluentValidation).
   - Query handlers use `.AsNoTracking()`.
   - No `catch(Exception)` in module code.
4. **Check localization.**
   - Zero hardcoded user-facing strings (backend or frontend).
   - All messages use `lockey_{scope}_{context}_{descriptor}`.
   - en + tr translation files at parity for new keys.
5. **Check observability.**
   - `ILogger<T>` injected; structured logging (PascalCase parameters, no string
     interpolation).
   - Success logged `Information`; expected business-rule failures `Warning`.
   - No secrets / PII in logs.
6. **Check API contracts.**
   - ApiEnvelope<T> wrapping on every endpoint.
   - DELETE returns `200 OK` with envelope (not 204).
   - Route format `/api/v{version}/{module}/{resource}`.
7. **Check frontend (if touched).**
   - TypeScript strict; no `any`.
   - TanStack Query for server state; Zustand minimal stores only.
   - Shared components from UX_UI_STANDARDS §9 — no rebuilds.
   - Detail pages use custom underline tab layout (NOT shadcn Tabs); max 5 tabs.
   - `useUnsavedChangesGuard` on edit forms; `FormField` wrapper on fields.
   - `npm run lint` passes.
8. **Check tests.** Unit tests per handler; architecture tests still green; integration
   tests for cross-module flows.
9. **Check migrations.** Additive-only in production paths; no destructive schema
   changes.

## Output

File `docs/analysis/reviews/code-reviews/YYYY-MM-DD-<pr-or-slug>.md` with sections:

- **Context** (PR, branch, author, scope).
- **Findings** — numbered; each tagged Blocker / Major / Minor / Nit with file:line and
  suggested fix.
- **Follow-up** — tasks to open (link `T-NNN`), ADRs to propose, standards to update.

## Related standards

- `docs/standards/CODE_REVIEW_STANDARDS.md`
- `docs/standards/CODING_STANDARDS.md`
- `docs/standards/FRONTEND_STANDARDS.md`
- `docs/standards/API_INTEGRATION_STANDARDS.md`
- `docs/standards/LOCALIZATION_STANDARDS.md`
- `docs/standards/OBSERVABILITY_STANDARDS.md`
- `docs/standards/UX_UI_STANDARDS.md`
- [ADR-016 — Module Tier Classification](../../../docs/decisions/ADR-016-module-tier-classification.md)
