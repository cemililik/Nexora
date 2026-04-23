# 0020 — Contact Extensions by Vertical Modules

## Status

Accepted

## Date

2026-04-22

## Context

The Tier-1 Contacts module provides the platform's unified contact registry. In earlier
drafts of the Contacts spec the core entity accumulated vertical-specific columns —
`is_donor` (Fundraising), `parent_of` (Education), `beneficiary_type` (NGO
case-management) — because each vertical needed one or two extra attributes on a contact.

[ADR-0016 Module Tier Classification](0016-module-tier-classification.md) makes this
pattern untenable:

- Tier-1 modules MUST remain domain-neutral. Donor/parent/beneficiary vocabulary is
  NGO/Education terminology and has no place in a module that ships to every tenant.
- Dependencies flow **downward only** — Tier-3 verticals may depend on Tier-1 Contacts,
  but Tier-1 Contacts MUST NOT learn about Tier-3 concepts.
- Editions are **orthogonal**: a tenant may install zero, one, or several verticals.
  Carrying NULL columns for the verticals a tenant has not bought is both a bad UX
  (empty fields in every contact form) and a schema bloat problem.

At the same time, verticals genuinely need extra per-contact data:

- **Fundraising** wants a donor status, lifetime-giving summary cache, preferred
  solicitation channel, tax-receipt preference.
- **Education** wants a role (parent / student / guardian) and linked-student IDs.
- **Healthcare (future)** wants patient MRN, primary physician.

We need a way for verticals to extend a core contact without mutating the core entity.

## Decision drivers

- **Tier isolation** — Tier-1 Contacts must not import Tier-3 schemas, packages, or
  vocabulary.
- **Install/uninstall cleanliness** — uninstalling a vertical edition must leave the
  core `contacts_contact` table unchanged.
- **Orthogonality** — multiple verticals may extend the same contact simultaneously,
  without colliding.
- **Indexability** — verticals need to index and query their extension data (e.g.
  "find all donors with lifetime giving > X").
- **Auditability** — extension rows must participate in the audit and GDPR pipelines
  like any other tenant data.

## Considered options

1. **Option A — JSONB column `vertical_extensions` on `contacts_contact`**
   - Summary: single JSONB column on the core entity, keyed by module ID.
   - Pros: one table, no joins, trivial to read.
   - Cons:
     - The Contacts module's migration history now churns whenever a vertical adds a
       field (even if the migration is "no-op schema, new JSON shape")
       — blurs the tier boundary.
     - JSONB indexing inside a shared column forces the core module to create and
       maintain every vertical's index.
     - Uninstall requires a cleanup job over every contact.
     - Row-level locking contention: one vertical writing contacts blocks another
       vertical writing the same contact.

2. **Option B — Side table `contact_extensions { contact_id, module_id, payload jsonb }`** — **chosen**
   - Summary: each vertical writes its own rows in a side table shared with other
     verticals, keyed by `(contact_id, module_id)`.
   - Pros:
     - Tier isolation preserved — the core module owns only the schema of the side
       table itself (id, contact_id, module_id, payload, timestamps); verticals own
       their payload shape, indexes, and audit events.
     - Uninstall is a single `DELETE WHERE module_id = :m` — surgical.
     - Verticals can add functional/GIN indexes over their own rows without the core
       module's involvement.
     - No lock contention between verticals updating different `module_id` rows of the
       same contact.
   - Cons:
     - One extra join in the 360-view; mitigated by a single batched query per contact.
     - `payload` is still JSONB; verticals must enforce their own schema at the
       application layer.

3. **Option C — Per-vertical side tables (e.g. `fundraising_contact_profile`)**
   - Summary: each vertical owns a dedicated `<module>_contact_profile` table keyed
     by `contact_id`.
   - Pros: fully typed, per-module migrations, best indexability.
   - Cons:
     - The core 360-view now has to know which vertical tables exist and how to read
       them, re-introducing the Tier-1 → Tier-3 dependency we are trying to avoid.
     - Aggregation requires a generic projection service anyway — we end up with
       Option B's indirection plus N extra tables.
     - Doesn't compose: two verticals cannot store related per-contact data next to
       each other without cross-module joins.

## Decision outcome

**Chosen: Option B.** Tier-3 (and Tier-4) modules contribute to a shared
`contact_extensions` side table owned by the Contacts module. The Contacts module
defines the table schema, a read-side DTO pipeline that aggregates extension rows by
`module_id`, and the cascade rules for soft-delete / GDPR anonymization. Verticals
define their own payload shape, indexes, and audit trail over their own rows only.

This preserves Tier-1 genericity (the core `Contact` entity contains no NGO, education,
or healthcare fields) while giving verticals a clean, isolated place to store their
per-contact data. It aligns with ADR-0016's dependency direction and makes
install/uninstall a local operation.

## Consequences

### Positive

- Core `Contact` entity stays domain-neutral; no Tier-3 vocabulary leaks upward.
- Verticals install and uninstall cleanly; their data is self-contained.
- Multiple verticals coexist on the same contact without schema conflict.
- 360-view becomes a generic aggregation pattern: "render a panel per extension row",
  driven by a vertical-supplied DTO mapper.

### Negative

- One extra join in the 360-view hot path. Mitigated by batching and by a projection
  cache keyed by `contact_id`.
- Payload schema evolution is a vertical responsibility — no platform-level guarantee
  of payload shape. Verticals MUST version their payload.
- Cross-vertical analytics (e.g. "donors who are also parents") require joining two
  `contact_extensions` rows; acceptable but not free.

### Neutral

- The core `contact_extensions` table becomes a new shared asset that every Tier-3
  module writes to; naming, permissions, and audit rules must be tight.

## Implementation notes

- **Schema (Contacts module migration):**

  ```sql
  CREATE TABLE contacts_contact_extensions (
      id             uuid PRIMARY KEY,
      contact_id     uuid NOT NULL REFERENCES contacts_contact(id) ON DELETE CASCADE,
      module_id      text NOT NULL,
      payload        jsonb NOT NULL,
      created_at     timestamptz NOT NULL,
      updated_at     timestamptz NOT NULL,
      UNIQUE (contact_id, module_id)
  );
  CREATE INDEX ix_contact_extensions_module ON contacts_contact_extensions (module_id);
  ```

- **Write path:** verticals use a repository from SharedKernel
  (`IContactExtensionRepository`) that enforces `module_id` matching the caller's
  module identity.
- **Read path (360-view):** Contacts module emits an extension DTO per row; verticals
  register an `IContactExtensionProjector` that maps their payload to a typed summary
  for their own UI panel.
- **Permissions:** read is gated by the vertical module's own `*.contact.view`
  permission; write by `*.contact.update`. Cross-vertical reads require explicit
  consent via the vertical's own matrix.
- **Audit:** each vertical records its own `update` / `delete` events on its extension
  rows per [audit-coverage.md](../standards/audit-coverage.md).
- **GDPR:** the core contact erasure command cascades to `contact_extensions` rows and
  invokes each vertical's anonymization hook (if registered) before hard-deleting.
- **Testing:** architecture test asserts that no Tier-1 code references any module ID
  string for known verticals; verticals asserted to write only their own `module_id`.

### IContactExtensionProjector contract

Defined in `Nexora.SharedKernel.Contacts`:

```csharp
public interface IContactExtensionProjector
{
    string ModuleId { get; }
    string PayloadVersion { get; }
    Task<IReadOnlyDictionary<string, object>> ProjectAsync(ContactId contactId, CancellationToken ct);
}
```

- Each vertical module registers one projector per extension type.
- `ProjectAsync` returns a display-ready dictionary (JSON-serializable) for the Contact 360 view — NOT the raw payload.
- Projectors MUST respect the caller's permission context (`ITenantContextAccessor` + `IPermissionService`) and redact fields the caller cannot read.

### Payload versioning

- Every extension payload has a `payload_version` field (semver-lite: `v1`, `v2`, ...).
- Vertical modules ship migration scripts for `v{N}` → `v{N+1}` as Hangfire jobs under `{module}:migrate-extensions-v{N}-to-v{N+1}`.
- Contacts module refuses to read extensions whose `payload_version` is not in the projector's supported set (structured error).
- Breaking schema changes require bumping the version; additive changes can stay on the current version.

### Projection cache

- Backend: `ICacheService` (not direct DaprClient).
- Cache key: `contacts:contact360:{contactId}`.
- TTL: 5 minutes.
- Invalidation: `ExtensionUpdated` integration event → `Contacts.Contact360Handler` → cache evict.

## References

- [ADR-0016 — Module Tier Classification](0016-module-tier-classification.md)
- [ADR-0008 — GDPR Deletion Strategy](0008-gdpr-deletion-strategy.md)
- [Contacts SPEC — Contact Extensions](../modules/tier-1-core/contacts/SPEC.md#contact-extensions)
