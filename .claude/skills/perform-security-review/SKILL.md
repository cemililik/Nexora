---
name: perform-security-review
description: Perform a security review of a Nexora change set or module slice — threat model, permission gating, multi-tenancy isolation, GDPR, secrets, logging hygiene — and file findings under docs/analysis/reviews/security-reviews/. Use when the user says "security review", "threat model", "pen test findings", or before any release that touches auth, payments, or PII.
---

# Perform Security Review

Structured security review of a Nexora slice. Output is immutable, dated evidence filed
under `docs/analysis/reviews/security-reviews/`.

## When to use

- Any change touching Identity, permission model, Keycloak integration, JWT handling.
- Any change touching payments (Stripe, iyzico, Param) or tax/receipt data.
- Any change touching PII flows (Contacts, GDPR erasure, audit PII capture).
- Any new external-facing surface (public forms, donor portal, parent portal).
- Quarterly baseline on the platform as a whole.

## Inputs

- Scope (module, endpoint group, PR, release).
- Threat model to apply (STRIDE by default).
- Deployment mode (`SaaS` vs. `OnPrem`) if relevant.

## Procedure

1. **Scope + threat model.** Name the assets (PII tables, tokens, secrets, signed
   documents) and the STRIDE categories in play.
2. **Check multi-tenancy isolation.**
   - Schema-per-tenant honored; no cross-tenant query without explicit
     `IgnoreQueryFilters()` + audit reason.
   - Tenant ID always sourced from `ITenantContextAccessor` (JWT claim) — never from
     request path or body.
   - Platform-scope permissions (ADR per Phase 1.5.2) hidden from tenant admins.
3. **Check authorization.**
   - Permission-based RBAC (`{module}.{resource}.{action}`) present on every endpoint.
   - Organization-scoped checks for multi-org tenants.
   - UI permission checks recognized as UX-only; backend enforces authoritatively.
4. **Check secrets + config.**
   - All secrets via `ISecretProvider` (Vault-backed in prod).
   - No secrets in appsettings, env vars, source, or `VITE_` / `NEXT_PUBLIC_` vars.
   - No secrets in logs.
5. **Check crypto + tokens.**
   - JWTs validated at APISIX gateway; backend re-validates claims.
   - No token storage in `localStorage`; httpOnly cookies or secure memory.
6. **Check GDPR.**
   - Article 17 erasure path wipes PII (cross-module event) while preserving
     `ConsentRecord` per Article 17(3)(e).
   - Export endpoint includes `CommunicationPreference` + `ContactRelationship`.
   - Audit PII scrubbed on erasure (before/after state).
7. **Check OWASP Top 10.**
   - Injection: parameterized EF Core queries (no raw SQL concat).
   - XSS: no `dangerouslySetInnerHTML`.
   - CSRF: APISIX + auth cookie flow.
   - SSRF: file uploads use presigned URLs, not server-side fetch.
   - Deserialization: strict JSON mapping; reject unknown fields on sensitive endpoints.
8. **Check integration events.**
   - Event payloads carry tenant ID; consumers verify via inbox dedup (ADR-014).
   - No PII in event metadata / headers; bodies encrypted in Kafka at rest.

## Output

File `docs/analysis/reviews/security-reviews/YYYY-MM-DD-<scope-slug>.md` with sections:

- **Context** — scope, deployment mode, threat model used.
- **Findings** — numbered; each tagged Critical / High / Medium / Low / Info. Include
  CWE / CVE / ATT&CK mapping where applicable. Cite file:line or endpoint.
- **Mitigations** — landed fixes, compensating controls, accepted risks (with approver).
- **Follow-up** — tasks, ADRs, standards updates.

## Related standards

- `docs/standards/` (will include `security-review.md` and `permissions.md` after
  Agent B's standards split)
- `docs/standards/CODING_STANDARDS.md`
- `docs/standards/OBSERVABILITY_STANDARDS.md` (logging hygiene)
- ADR-002 (schema-per-tenant), ADR-004 (permission seeding), ADR-006 (permission-based
  authorization), ADR-008 (GDPR deletion)
