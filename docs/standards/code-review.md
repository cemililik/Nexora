# Code Review Standard

Derives from: [ADR-006 Permission-Based Authorization](../decisions/ADR-006-permission-based-authorization.md)
(for SEC checklist items), [ADR-014 Distributed Consistency Patterns](../decisions/ADR-014-distributed-consistency-patterns.md)
(for ARCH consistency-tier items). Ported from legacy `CODE_REVIEW_STANDARDS.md`.

Code reviews are the primary quality gate before code enters `development`. Every pull request
MUST be reviewed before merge. For full narrative and templates see the legacy file until
Agent C folds it into an ADR-referenced artifact; this document is the operative checklist.

## 1. Review Principles

1. **Standards first** — every finding cites a specific standard, not preference.
2. **Severity accuracy** — classify by impact, not effort to fix.
3. **Actionable fixes** — every finding includes a concrete fix or direction.
4. **No false positives** — verify each finding against the actual code.
5. **Regression awareness** — on fix commits, treat the fix as a potential source of new bugs.
6. **Completeness** — review all changed files, not just the interesting ones.

## 2. Severity

| Severity | Label | Criteria | Blocker |
|----------|-------|----------|---------|
| CRITICAL | `[CRITICAL]` | Security vulnerability, data integrity risk, zero-tolerance violation | Yes |
| MAJOR | `[MAJOR]` | Architectural problem, business logic error, missing test for critical path | Yes |
| MINOR | `[MINOR]` | Code quality issue, minor deviation, missing optimization | No |
| SUGGESTION | `[SUGGESTION]` | Optional improvement, future consideration | No |

## 3. Zero-Tolerance Rules (Always CRITICAL)

| Rule | Source |
|------|--------|
| Hardcoded user-facing string (no `lockey_` key) | [localization.md](localization.md) |
| Secret in source or config | [architectural-principles.md](architectural-principles.md) §Infrastructure |
| Token stored in localStorage | [code-style.md](code-style.md) §Security |
| `dangerouslySetInnerHTML` usage | [code-style.md](code-style.md) §Security |
| `any` type in TypeScript | [code-style.md](code-style.md) §TypeScript |
| `catch(Exception)` in module code | [architectural-principles.md](architectural-principles.md) §Error Model |
| Cross-module direct reference | [architectural-principles.md](architectural-principles.md) §Module Boundaries |
| Missing tenant isolation in cache/query | [architectural-principles.md](architectural-principles.md) §Multi-Tenancy |
| PII/secret in log output | [architectural-principles.md](architectural-principles.md) §Observability |
| API endpoint without `[Authorize]` or explicit `[AllowAnonymous]` | [permissions.md](permissions.md) |
| Tenant ID from request body/query | [architectural-principles.md](architectural-principles.md) §Multi-Tenancy |
| `NEXT_PUBLIC_` env var containing a secret | [code-style.md](code-style.md) §Security |
| Auth gate not checking `token.error` | [code-style.md](code-style.md) §Security |
| `as unknown as T` double cast | [code-style.md](code-style.md) §TypeScript |
| Raw SQL string concatenation | [security-review.md](security-review.md) |
| Destructive migration without blue/green | [commit-style.md](commit-style.md) §Migrations |

## 4. 10-Category Checklist

Full item-level text lives in legacy `CODE_REVIEW_STANDARDS.md` §4. The 10 categories:

1. **Security (SEC-1..16)** — secrets, PII in logs, XSS, token storage, SQL injection,
   input validation, SSRF, tenant isolation, auth attributes, JWT claim access, public
   env-var hygiene, auth-gate error state, dependency CVEs.
2. **Architecture (ARCH-1..20)** — module boundaries, CQRS + validators, Result pattern,
   server/client separation, state-management hierarchy, `ApiEnvelope<T>`, repository layer,
   `'use client'` correctness, `NexoraJob` idempotency, table prefixing, domain events on
   aggregate root, versioned endpoints, `AsNoTracking`, co-located `loading/error.tsx`,
   no Server Actions, factory `QueryClient`, idempotent event handlers, soft-delete usage.
3. **Localization (L10N-1..9)** — `lockey_` everywhere, key format, en+tr parity,
   `WithMessage`/`DomainException`/`Result` usage, frontend resolver choice, locale-aware
   redirects, RTL logical properties.
4. **Type Safety (TS-1..10)** — no `any`, no unsafe assertions, strongly-typed IDs, record
   DTOs, boundary validation, no hardcoded config, no duplicated enums, Zod for API
   responses, no `as unknown as T`, factory-method value objects.
5. **Testing (TEST-1..15)** — naming, AAA, critical-path coverage, deduplication,
   determinism, minimal mocks, specific assertions, co-located files, edge cases, stable
   selectors, auth paths, idempotency, `token.error` path, permission guards, external
   failure paths.
6. **Observability (OBS-1..9)** — structured logging, log levels, success + failure logging,
   correlation ID, error boundaries, `extractApiError`, custom `ActivitySource`,
   module metrics, no `console.*` in prod.
7. **Code Quality (CQ-1..10)** — naming, no dead code, DRY, stable refs, no render-time
   side effects, `type="button"`, ARIA, C# conventions, XML docs, no inline style.
8. **Performance (PERF-1..9)** — pagination, cache TTL + tenant keys, `staleTime`, lazy
   routes, no unnecessary re-renders, no N+1, `next/image`, `next/font`, bundle size.
9. **Configuration & Infrastructure (CFG-1..7)** — `.env.example` docs, `IValidateOptions`,
   `ISecretProvider`, image naming, additive-only migrations.
10. **API Contract / DB Migration / Docs (ACS-1..6, DB-1..8, DOC-1..7)** — backwards
    compatibility, additive migrations, typed-ID converters, global query filters, PR
    description quality, markdownlint, Mermaid diagrams, ADR immutability.

## 5. Workflow

1. Author runs self-review (§6.1) and pushes.
2. CI runs (build, lint, markdownlint, tests, architecture tests).
3. Reviewer assigned. Security-sensitive modules require **2 reviewers** including the
   module owner.
4. Reviewer works the full checklist against changed files.
5. Findings documented as inline comments + review summary.
6. Verdict: `APPROVED` (zero CRITICAL/MAJOR), `CHANGES_REQUESTED`, or `COMMENT`.
7. Author fixes and requests re-review. Reviewer re-checks with regression awareness.
8. Squash merge to `development` (never `main` / `test` directly — see
   [commit-style.md](commit-style.md)).

Security-sensitive modules: `Identity`, `Donations`/`Payments`, `Infrastructure`,
`Middleware`, any migration touching the `public` schema.

## 6. Author Self-Review Gate

Before requesting review the author MUST have confirmed:

### 6.1 Backend
- All new API endpoints have `[Authorize]` or `[AllowAnonymous]` with a justification comment.
- Tenant ID sourced from `ITenantContext`, never from input.
- No `catch (Exception)` in module code.
- Every command has a validator with `lockey_` messages.
- All `Result.Failure`, `DomainException`, `.WithMessage()` use `lockey_` keys.
- `ILogger<T>` used with structured parameters, no interpolation.
- `AsNoTracking()` on read-only queries.
- Domain events raised via `AddDomainEvent()`.
- Migrations: no `DROP`, no non-nullable column without `DEFAULT`.
- New NuGet packages checked for CVE + license.

### 6.2 Frontend
- No `any`, no `as unknown as T`.
- No hardcoded user-facing strings; `lockey_` keys exist in `en` + `tr`.
- No `dangerouslySetInnerHTML`, no tokens in `localStorage`, no secret in `NEXT_PUBLIC_`.
- Auth gate checks `!token || token.error === 'RefreshAccessTokenError'`.
- `'use client'` only where needed.
- `next/image`, `next/font`.
- `QueryClient` in factory.
- No stray `console.log` / `console.error`.
- `loading.tsx` / `error.tsx` for new route segments.
- TanStack Query v5 API (`isPending` for mutations).

## 7. Review Artifact Format

```markdown
# Code Review: <Feature / PR Title>

## Metadata
- Date, Reviewer, Branch, Commit, Scope

## Summary
| Category | Total | Critical | Major | Minor | Suggestion |
| ...      |       |          |       |       |            |

**Verdict**: APPROVED | CHANGES_REQUESTED | COMMENT

## Findings
### [SEVERITY] CATEGORY-# — File: Title
**File**, **Lines**, **Checklist Item**
**Current code**, **Problem**, **Required fix**, **Standard reference**
```

Rules:

- Summary table count MUST match detailed findings.
- Line numbers MUST be accurate.
- Every finding cites a checklist item.
- On fix reviews include a "Prior Review Status" table with statuses
  Resolved / Partially resolved / Regression / Open.
- The review document itself passes markdownlint.
