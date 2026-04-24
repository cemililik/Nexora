# 0029 — `cap.blocked` short-circuits lower layers in the compliance-config resolver

## Status

Accepted

## Date

2026-04-24

## Supersedes

- [ADR-0025](0025-org-scoped-compliance-config-with-platform-caps.md) —
  original three-tier resolver (`cap.forced > org > tenant > cap.default`).
  Retained unchanged for historical record; this ADR replaces the resolution
  ladder it described.

## Context

ADR-0025 introduced the three-tier configuration resolver with the ladder
`cap.forced > org > tenant > cap.default`. The cap provider could block
*writes* (`SetOrgOverrideAsync` throws `ComplianceCapViolationException`
when `Allowed = false`), but *reads* still consulted the org + tenant
layers first and only fell through to `cap.default` when neither had a
value. That leaves one correctness gap the implementation surfaced during
T-019 review (commit `576ff65`):

- A tenant had an org-level override persisted (say `gdpr.hard_delete.enabled = true`).
- Later, the platform cap tightens to `Allowed = false` for that key.
- With the original ladder, `GetAsync<bool>(key)` still returns `true` —
  the stale override leaks through because the cap only governed writes.

For an EU tenant sitting behind the ADR-0023 provisioning gate, this is
an Article 17 / compliance-cap bypass. The resolver cannot honestly claim
"the platform cap disables this key" while serving the old value.

ADR-0025 shipped as Accepted and `576ff65` adjusted the resolver to
short-circuit when `cap.Allowed = false`. An inline "Amendment 1" block
initially documented the behaviour change inside ADR-0025 itself, but the
project's ADR-immutability rule (decisions/README.md §Immutability) treats
amendments as non-normative clarifications; a behavioural change — the
ladder is different now — needs a superseding ADR so the git history can
answer "what decision was in force at time T?" from the ADR files alone.

## Decision drivers

- Article 17 / ADR-0023 EU-tenant gate MUST hold: when the cap disables a
  key, the resolver MUST NOT surface a stale override from a pre-cap era.
- ADR-immutability rule: normative changes warrant a superseding ADR, not
  an in-place amendment.
- Backward compatibility for non-block cap states (`Allowed = true, Forced = false`)
  — the common path is unchanged.
- Call-site predictability: `cap.Allowed = false` with `cap.Value = null`
  has a defined resolved value rather than "no answer".
- Test coverage: the new behaviour has distinct tests that fail under the
  old ladder; flipping the ladder back would fail CI.

## Considered options

### 1. Short-circuit on `cap.Allowed = false` *(chosen)*

Replace the ladder with:
`cap.forced > (cap.blocked → cap.value) > org > tenant > cap.default`

Reads where `cap.Allowed = false`:

- If `cap.Value` is supplied, deserialise and return it (winner =
  `ResolutionLayer.Cap`).
- If `cap.Value` is `null`, return `default(T)`
  (`false` for `bool`, `null` for nullable and reference types, `0` for
  `int`, etc.) — still reported as `ResolutionLayer.Cap`.

Org and tenant layers are skipped entirely during this branch. The audit
row for the prior override remains in
`platform_compliance_policy_audit` for forensics; the resolver simply
doesn't surface it.

- Pros
  - Closes the Article 17 gap by construction.
  - Reads and writes are now symmetric: if the cap blocks writes, it also
    blocks reads.
  - Implementation is a single branch in `DatabaseConfigurationResolver.ResolveAsync`.
- Cons
  - **Breaking semantic** for call sites that depended on the pre-change
    read behaviour. Mitigated by the grep: there is exactly one such call
    site (T-019's `RequestGdprDeleteHandler`), and its post-change
    behaviour is the intended one (hard-delete stops under a forced-disable
    cap). No other production caller exists yet.
  - `cap.Value = null` resolves to `default(T)` — a call site that relied
    on "org value" returning `true` will now get `false`. Callers MUST
    treat the resolved value as authoritative; they MUST NOT compare
    against a cached "expected org value" to decide whether the cap is
    active.

### 2. Keep the old ladder; block only writes

Leave reads untouched, keep documenting the stale-override risk.

- Pros
  - Zero code change.
- Cons
  - Leaves the Article 17 gap open. **Rejected.**

### 3. Auto-purge stale overrides on cap tighten

When `cap.Allowed` flips to `false`, fire a background job that deletes
matching org-override rows.

- Pros
  - Reads stay simple; the ladder is unchanged.
- Cons
  - Destroys forensic state (the override row says who enabled the key
    and when — compliance auditors rely on that).
  - Job ordering vs. resolver reads is a race; a tenant request landing
    between "cap tightens" and "purge completes" still returns the stale
    value. **Rejected.**

## Decision outcome

Adopt Option 1. The resolver's ladder becomes:

```text
cap.forced          → cap.Value deserialised (wins unconditionally)
cap.blocked         → cap.Value deserialised OR default(T) (new in this ADR)
org override        → deserialised (only when neither cap state above)
tenant default      → deserialised (only when no org override)
cap.default         → cap.Value deserialised (only when no tenant default)
neither             → default(T)
```

The behaviour is normative: every caller on `IConfigurationResolver.GetAsync`
MUST assume these semantics.

## Consequences

### Positive

- EU-tenant compliance flow is now honest: the cap is the authoritative
  gate on both reads and writes.
- T-019's `gdpr.hard_delete.enabled` reads as `false` under a forced-disable
  cap even if a pre-cap org override said `true` — the intended Article 17
  behaviour.
- Audit trail stays intact — org overrides are preserved in the audit
  table; the resolver simply ignores them when the cap blocks.

### Negative

- `cap.Value = null` resolves to `default(T)` — a subtle behaviour that
  future call sites need to learn. Documented on
  `IConfigurationResolver` XML docs and in the
  `IConfigurationResolver.GetAsync` parameter remarks.
- Breaking semantic compared to ADR-0025. Mitigation: the only
  production consumer is T-019; its behaviour is validated by
  `RequestGdprDeleteHandler` integration tests.

### Neutral

- No schema change; no new tables, no new columns. The audit table
  continues to capture the prior override for any forensic purpose.

## Implementation notes

- Already shipped in commit `576ff65` (v3 review batch of T-019 / T-020).
- Resolver branch lives in
  `src/Nexora.Infrastructure/Configuration/DatabaseConfigurationResolver.cs`
  — the `!cap.Allowed` block before the `orgPresent` / `tenantPresent`
  fall-through.
- `IConfigurationResolver` interface XML doc (`src/Nexora.SharedKernel/...`)
  carries the `default(T)` behaviour note so IntelliSense surfaces it to
  callers.
- Test coverage:
  - `Get_CapAllowedFalse_IgnoresLowerLayers_EvenIfPresent` —
    asserts the short-circuit skips a persisted org override.
  - `SetOrgOverride_CapDisallows_AuditRecordsPriorOverrideAsOldValue` —
    asserts the audit row captures the prior override as `OldValue`
    on a rejected write.
  Both in `tests/Nexora.Infrastructure.Tests/Configuration/DatabaseConfigurationResolverTests.cs`.

## References

- ADR-0025 — Superseded by this ADR.
- ADR-0023 — NMP billing model / EU-tenant provisioning gate.
- ADR-0008 — GDPR deletion strategy.
- `docs/decisions/README.md` §Immutability — "normative changes need a
  superseding ADR" rule this ADR implements.
- Commit `576ff65` — initial implementation (the inline Amendment 1 in
  ADR-0025 pointing here has been removed; this ADR is the canonical
  description going forward).
