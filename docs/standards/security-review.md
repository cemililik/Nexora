# Security Review Standard

Derives from: [ADR-002 Multi-Tenancy](../decisions/ADR-002-multi-tenancy.md),
[ADR-006 Permission-Based Authorization](../decisions/ADR-006-permission-based-authorization.md),
[ADR-008 GDPR Deletion Strategy](../decisions/ADR-008-gdpr-deletion-strategy.md),
[ADR-009 Audit Repository Pattern](../decisions/ADR-009-audit-module-repository-pattern.md).

A security review is a focused pass that complements the standard code review
([code-review.md](code-review.md)). It is mandatory for:

- Any change under `Nexora.Modules.Identity`, `Nexora.Infrastructure` (secrets, cache,
  config), payment/donation modules, middleware in `nexora-admin` / `nexora-portal`, or any
  migration touching `public` schema.
- Any change introducing a new external integration (payment gateway, SSO, webhook).
- Any change touching permission seeds, JWT claim handling, or tenant resolution.

This document is a **lens**, not a comprehensive security manual. It maps OWASP Top 10
(2021), multi-tenant isolation, and Nexora-specific concerns to checklist items the reviewer
must clear.

## 1. OWASP Top 10 Lens (2021)

| OWASP | What to verify in Nexora code |
|-------|-------------------------------|
| A01 Broken Access Control | Every endpoint has `[Authorize]` or justified `[AllowAnonymous]`; permission check uses `{module}.{resource}.{action}` (see [permissions.md](permissions.md)); no IDOR — object access gated by tenant + org filters. |
| A02 Cryptographic Failures | No hand-rolled crypto; use platform primitives. PII encrypted at rest where flagged. TLS enforced on all inbound/outbound. |
| A03 Injection | No raw SQL interpolation — parameterized queries only. EF LINQ compiles to parameters; `FromSqlRaw` only with interpolated helpers. User input validated at boundaries. |
| A04 Insecure Design | Consistency tier chosen per [ADR-014](../decisions/ADR-014-distributed-consistency-patterns.md). Idempotency keys on payment flows. Pending-first writes before external calls. |
| A05 Security Misconfiguration | `appsettings.{Environment}.json` has no secrets; `ISecretProvider` used throughout. CORS whitelist explicit. Default Keycloak realm settings verified. |
| A06 Vulnerable Components | New NuGet/npm packages checked against CVE DB and license allowlist. |
| A07 Identification & Authentication Failures | JWT validated at APISIX; JWT claims accessed via typed extension methods. Refresh-token rotation respected. Auth gate checks `token.error === 'RefreshAccessTokenError'`. |
| A08 Software & Data Integrity Failures | Migrations are additive; integration-event handlers idempotent; outbox pattern used for cross-service writes (ADR-005). |
| A09 Logging & Monitoring Failures | Security events (login, permission change, tenant create/delete, failed auth) audited — see [audit-coverage.md](audit-coverage.md). Structured logs; no PII/secrets in log output. |
| A10 SSRF | Any outbound URL derived from user input validated against an allowlist; URL shortener and file-import modules apply. |

## 2. Multi-Tenant Isolation Checks

Per [ADR-002](../decisions/ADR-002-multi-tenancy.md):

- [ ] Tenant ID sourced from `ITenantContext` / JWT claim — never from request body, query,
  header, or route parameter.
- [ ] Every new EF Core entity that is tenant-scoped has a global query filter on
  `TenantId` and `IsDeleted`.
- [ ] Every new cache key uses the module-perspective format `{module}:{entity}:{id}` — the
  infrastructure layer prepends the tenant ID. No manual tenant prefixing.
- [ ] Background jobs derive tenant context through `NexoraJob<T>` — not from captured
  variables or request-scoped services.
- [ ] Integration events carry `TenantId` and consumers verify it before acting.
- [ ] Cross-tenant foreign keys are forbidden; architecture test or code review catches
  violations.
- [ ] `IgnoreQueryFilters()` usage is reviewed — only allowed for admin/audit scenarios.

## 3. JWT & Permission Gate Review

- [ ] New endpoint has `[Authorize]` with appropriate `Policy` or `Permissions` attribute.
- [ ] `[AllowAnonymous]` carries a one-line justification comment explaining why.
- [ ] Permissions declared in the module's migration seed (see
  [permissions.md](permissions.md) §Seeding) and resolvable by name in tests.
- [ ] Permission check performed **before** any state mutation or read of sensitive data.
- [ ] Organization-scoped permissions enforce the `organization_id` filter.
- [ ] Frontend route guard and backend policy agree on the permission string.

## 4. Secrets Handling

- [ ] No secret in source, `appsettings*.json`, environment variables (except the Dapr
  secret-store bootstrap), or logs.
- [ ] Secret obtained via `ISecretProvider.GetSecretAsync("nexora/{category}/{name}", ct)`.
- [ ] Secret never printed to log, trace, or error response.
- [ ] Secret rotation documented for new integrations — every secret has an owner and a
  rotation cadence.
- [ ] PR body contains no secret value.
- [ ] **Leakwatch** secret scan passes on the diff (filesystem + git history). Nexora uses
  Leakwatch (in-house, MIT; 63 detectors + 53 verifiers; SARIF output to GitHub Code Scanning)
  as the single sanctioned secret scanner — do not substitute gitleaks / trufflehog.
  - CI integration: `cemililik/leakwatch-action@v1`, scan-type `git`, `min-severity: medium`,
    `sarif-upload: true`, `only-verified: true` for fail-build gates.
  - Reference: [Leakwatch CI/CD Integration Guide](https://github.com/cemililik/leakwatch) and
    the local guides under `/Leakwatch/docs/guides/ci-cd-integration.md`.
  - Pre-commit hook: run Leakwatch in `fs` scan mode against staged files (see
    `ci-cd-integration.md §5 Pre-commit Hook`).

## 5. Dependency / CVE Review

For every new or upgraded NuGet / npm package:

- [ ] Check known CVEs (`dotnet list package --vulnerable`, `npm audit`, GitHub advisory DB).
- [ ] Confirm license is compatible with commercial redistribution
  (MIT / Apache-2.0 / BSD / MPL-2.0 acceptable; GPL/AGPL blocked without legal sign-off).
- [ ] Assess bundle-size impact for frontend packages > 10 kB minified.
- [ ] Prefer well-maintained packages (last commit < 12 months, > 10k weekly downloads on
  npm, or equivalent NuGet signal).

## 6. Crypto & PII Handling

- [ ] Passwords never handled by Nexora directly — Keycloak owns credential storage.
- [ ] Any symmetric/asymmetric key originates from the secret store; no keys in source.
- [ ] Hashing for non-credential use (file integrity, idempotency keys) uses `SHA-256` or
  higher — never `MD5` or `SHA-1`.
- [ ] PII (email, phone, address, national ID, financial data) classified and tagged in the
  entity. Audit coverage for PII reads — see [audit-coverage.md](audit-coverage.md).
- [ ] PII never appears in URLs, log output, trace span names, or exception messages.
- [ ] GDPR deletion flow per [ADR-008](../decisions/ADR-008-gdpr-deletion-strategy.md)
  respected for any new PII store.

## 7. Audit Coverage Cross-Reference

Any new operation that mutates data or reads sensitive data MUST be classified in
[audit-coverage.md](audit-coverage.md) §Baseline Matrix and implemented via the audit
repository pattern
([ADR-009](../decisions/ADR-009-audit-module-repository-pattern.md)).

## 8. Artifact Format

Security reviews use the same structured artifact as code reviews
([code-review.md](code-review.md) §7) but with an additional prefix summary:

```markdown
# Security Review: <Feature / PR Title>

## Threat Surface Summary
- Entry points changed: ...
- Trust boundaries crossed: ...
- Secrets / PII introduced: ...
- External integrations: ...

## OWASP Coverage
| OWASP | Status | Notes |
| A01   | ok / risk / n/a | ... |
| ...   |        |       |

<standard code review body follows>
```

TODO(maintainer): add a `.claude/skills/perform-security-review` skill that scaffolds this
document.
