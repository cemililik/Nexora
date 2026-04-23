# 0027 — Production schema-migration strategy

## Status

Proposed  <!-- Proposed | Accepted | Superseded by NNNN -->

## Date

2026-04-23

## Context

Nexora's dev environment evolves schema through `DevelopmentSeed.ApplySchemaUpdatesAsync`
in `Nexora.Host` — an idempotent array of raw SQL statements
(`ADD COLUMN IF NOT EXISTS`, `CREATE TABLE IF NOT EXISTS`, …) applied on every
container start. The mechanism is explicitly guarded by
`app.Environment.IsDevelopment()` and has served Phase 1 / Phase 1.5 well:
fast iteration, no migration-file churn, trivial rollback via
`docker compose down -v`.

But the platform is approaching the boundary between dev / preview and
production:

- **[`docs/standards/schema-migration.md`](../standards/schema-migration.md)**
  notes "Production strategy — TBD" at the top, and §6 routes production DDL
  through that future ADR.
- **[`CLAUDE.md`](../../CLAUDE.md) — Database section** repeats the TBD note
  and points at the archived `INFRASTRUCTURE_STANDARDS.md` as an interim
  reference that "may be out of date".
- **[`docs/operations/migration-orchestration.md`](../operations/migration-orchestration.md)**
  already assumes EF Core migrations with advisory locks, module dependency
  ordering, and per-tenant rollout — but the ADR that justifies the tool
  choice and the dev-to-prod bridge has never been written.
- **[T-011](../analysis/tasks/phase-2/T-011.md)** in Phase 2 is titled
  "MigrationRunner.MigrateAllModulesAsync implementation" and is blocked on
  this decision.

A code-review finding
([`docs/code-reviews/2026-03-29-comprehensive-opus.md`](../code-reviews/2026-03-29-comprehensive-opus.md))
already flagged the drift between `ApplySchemaUpdatesAsync` and whatever the
production tool will be as a risk. The drift is real: the T-018 integration
test surfaced that raw-SQL ALTER statements in dev shadow EF's model
(`HasFilter` referring to non-existent columns, T-021), and there is no
enforcement that a dev DDL change ever makes it into a production migration
file.

Phase 2 (Enterprise Core) cannot start until the tool choice, the dev-to-prod
bridge, and the CI enforcement are captured in an Accepted ADR. Merging
Phase 2 modules without that discipline is how a 10k-migration sprawl
(Metasfresh / Dolibarr pattern flagged in our own code reviews) begins.

## Decision drivers

- Preserve dev velocity — `docker compose down -v && up` must keep producing a
  correct schema in under a minute.
- Support schema-per-tenant rollout — every production schema change runs
  per-tenant, in dependency order, with safe concurrency caps. This is
  already the contract assumed by `migration-orchestration.md`.
- Additive-only at the tool level — no `DROP COLUMN`, `RENAME`, or type
  narrowing may leak into production; CI fails the PR, not the deploy.
- Single source of truth — dev DDL and production DDL MUST be inspectable
  from the same repo in the same PR. Hidden state (manual psql, external
  migration DBs) is disqualifying.
- Rollback semantics — production migrations MUST have an explicit,
  documented rollback path (even if "rollback is a forward migration"),
  because schema-per-tenant + rolling upgrade makes traditional "step back
  one" impossible for half-migrated tenants.
- Team expertise — the team's .NET/EF Core experience is deep; JVM tooling
  expertise is not. Operational familiarity is a feature, not a nice-to-have.
- ADR-0002 (schema-per-tenant) and ADR-0003 (deployment strategy — rolling,
  additive-only) are constraints, not options.

## Considered options

### 1. EF Core Migrations, module-scoped, with an explicit dev-to-prod bridge *(chosen)*

Every module owns a `Migrations/` folder and an `__ef_migrations_{module}`
history table inside each tenant schema. Production schema evolution flows
entirely through EF Core migrations applied by `MigrationRunner` (T-011) per
the `migration-orchestration.md` runbook.

Dev iteration stays on `ApplySchemaUpdatesAsync`, but a **release gate** —
enforced by a CI architecture test — asserts that every DDL statement in
`ApplySchemaUpdatesAsync` has a corresponding EF migration in the module
that owns the table. When a dev DDL lands, the same PR (or a follow-up PR
before the release cut) generates the matching `dotnet ef migrations add …`
output; at release time `ApplySchemaUpdatesAsync` is trimmed to statements
that are *only* needed for the dev fast-reset cycle.

- Pros
  - Uses the team's existing expertise; no new tool to learn/operate.
  - Per-module migration history fits the modular-monolith boundary (no
    centralised migration god-object, matches ADR-0001 + ADR-0016).
  - Additive-only enforceable via a Roslyn-lite architecture test
    (`MigrationTests.cs`) that parses every `Migration.Up` and rejects
    `DropColumn`, `RenameColumn`, type-narrowing `AlterColumn`.
  - Idempotent migration runner + advisory locks already designed in
    `migration-orchestration.md` §2 — this ADR formalizes it.
  - Rollback is a forward migration: `20260601_RemoveX` is just another
    additive PR, reviewed the same way.
- Cons
  - Dev-to-prod bridge is real discipline: engineers must remember to author
    the migration alongside the `ApplySchemaUpdatesAsync` change. Mitigated
    by the CI gate (see §Implementation notes).
  - EF Core's migration bundles can get chatty for pure-data changes — the
    runbook requires a follow-up `ef bundle` step per release cut.
  - Cross-module migration ordering relies on `IModule.Dependencies` +
    `MigrationRunner`'s topological sort; a module owner who forgets a
    dependency can produce a failing migration on fresh tenants. Caught by
    the provisioning-smoke test in CI.

### 2. Liquibase / Flyway

Introduce Liquibase (or Flyway) as the canonical migration tool; `dotnet ef`
stays for the scaffolding step but the production runner is the third-party
migration engine.

- Pros
  - Industry-standard, battle-tested on multi-tenant Postgres.
  - Declarative XML/YAML changelogs are tooling-agnostic (non-.NET languages
    can read them).
- Cons
  - Two migration systems (EF Core scaffolding + Liquibase execution) == a
    permanent sync problem. Either developers author changes twice, or we
    rewrite EF's output to changelogs — both are drift generators.
  - Team has no operational experience with Liquibase; on-call burden is a
    real cost the platform has not yet paid.
  - Metasfresh's 10k-migration sprawl (flagged in our own review notes at
    [`docs/code-reviews/metasfresh-analiz-20260330.md`](../code-reviews/metasfresh-analiz-20260330.md))
    is a cautionary case study: the tool does not save you from the
    discipline problem that the dev-to-prod bridge solves head-on.
  - Changelog files live outside the C# module they belong to — harder to
    enforce ownership / module-boundary review.
- **Rejected** — the costs are real, the upside (tooling-agnostic) is
  theoretical for a .NET-centric platform.

### 3. Keep `ApplySchemaUpdatesAsync` as the production mechanism too

Ship the dev raw-SQL array as the production migration pipeline. Idempotent
by construction; no tooling to learn.

- Pros
  - Zero incremental work.
- Cons
  - **No version history** — "what DDL ran in production at release 1.2.3?"
    is unanswerable without git archaeology.
  - **No rollback trail** — there is no "ran at T0 in tenant X" record.
  - **No additive-only CI enforcement** — raw SQL strings aren't parseable
    by our architecture tests; `DROP COLUMN` would land without tripping a
    check.
  - **No migration bundle artifact** — SREs cannot preview the exact DDL a
    release will run before cutting it.
  - Would be a loud failure under any compliance audit. **Rejected** — this
    was never a serious option; it is listed only to document *why*
    continuing the dev pattern into production is unacceptable.

### 4. Bespoke versioned SQL files per module (à la Dapper/Dotnet scripts)

Keep raw SQL but version it: `Migrations/V001__AddContacts.sql`,
`V002__AddGdprAudit.sql`, …; a hand-rolled runner applies pending files by
sorting on the version prefix.

- Pros
  - Readable, grep-able, no EF magic.
  - Very low abstraction tax for simple changes.
- Cons
  - Every EF-flavoured feature (shadow properties, computed columns, type
    conversions) has to be translated by hand and kept in sync with the EF
    model. The drift problem that T-021 surfaced returns with interest.
  - We'd be rebuilding 70% of EF Core migrations without any of its
    ecosystem tooling (bundles, snapshots, diff generation).
  - No scaffolding support — every change is a greenfield SQL write.
- **Rejected** — the "readable" advantage vanishes the moment the schema is
  non-trivial; bespoke tooling becomes its own operational debt.

## Decision outcome

**Adopt Option 1 — EF Core Migrations, module-scoped, with an explicit
dev-to-prod bridge and CI-enforced additive-only policy.**

The dev path (`ApplySchemaUpdatesAsync` in `DevelopmentSeed`) stays exactly
as it is, because the single-commit `docker compose down -v && up` cycle is
load-bearing for local iteration speed. The production path is EF Core
migrations, applied by `MigrationRunner` per `migration-orchestration.md`.
The bridge between them is a **release-gate CI test** — every DDL statement
in `ApplySchemaUpdatesAsync` must be backed by a migration in the module
that owns the table before a release cut; if it is not, the CI job fails
the release PR, not the deploy.

This formalizes what `migration-orchestration.md` already assumed and
unblocks T-011. The dev-vs-prod discipline is the same shape as the
"proposed ADR blocks implementation" discipline we already practice
elsewhere — engineers already know how to honour it.

## Consequences

### Positive

- Phase 2 modules (CRM, Finance, Subscription, Projects) can ship with
  confidence; migration orchestration is a solved problem.
- Additive-only is mechanically enforced, not just a review checklist.
  `DropColumn` cannot land silently.
- SREs get a pre-deploy migration-bundle artifact (`dotnet ef migrations
  bundle --project Nexora.Modules.X`) they can dry-run against a clone.
- The `ApplySchemaUpdatesAsync` drift problem (T-018 / T-021) is structurally
  preventable: a DDL line with no matching migration at release time fails
  CI.
- Schema-per-tenant isolation continues to hold — each tenant runs its own
  migration sequence with an advisory lock.

### Negative

- Engineers now maintain a second artifact (the EF migration) alongside the
  dev DDL statement for the lifetime of the change. The CI gate makes the
  cost unmissable, but it is still a cost.
- `MigrationRunner` (T-011) is a non-trivial piece of infrastructure — it
  takes time to build, test, and harden.
- `dotnet ef` tooling becomes a contributor-environment requirement;
  documented in the contributor guide.
- First migration bundle per release adds deploy time (~5–30 s per tenant
  for a small migration, longer for index rebuilds). Budgeted in
  `migration-orchestration.md` §2.2.

### Neutral

- The archived `docs/_archive/standards-legacy/INFRASTRUCTURE_STANDARDS.md`
  ceases to be an "interim pointer for production" — its references in
  `schema-migration.md` §6 and CLAUDE.md are cleared and retargeted at this
  ADR once accepted.

## Implementation notes

Concrete pointers for implementers:

- **Module migration layout**:
  - `src/Modules/Nexora.Modules.{Name}/Migrations/` owns the module's
    migrations; history table is `__ef_migrations_{module}` inside each
    tenant schema (not `public`, not the dev `tenant_00000000-…` default).
  - Naming: `{yyyyMMddHHmm}_{PascalCaseDescription}.cs` (EF's default).
  - Each `Migration.Up` / `Down` pair carries the `[Migration("id")]` attribute
    EF generates; no custom conventions.
- **MigrationRunner (T-011)**:
  - Resolves `IEnumerable<IModule>`, topologically sorts by
    `IModule.Dependencies` (already done for `DemoDataSeeder` in T-005;
    refactor to a shared helper).
  - Per-tenant: `pg_advisory_lock(hashtext('migrate:' || tenant_id))` →
    apply pending migrations per module in dependency order → release lock.
    Max 10 parallel tenants, page size 50.
  - Writes outcomes to `platform_migration_failures` on failure; successful
    tenants update `tenants.schema_version`.
- **CI additive-only test** (`tests/Nexora.Architecture.Tests/MigrationTests.cs`,
  delivered with T-011): parses every `Migration.Up` method body in every
  module assembly, rejects `DropColumn`, `DropTable`, `RenameColumn`,
  `RenameTable`, and `AlterColumn(... isNullable: false, defaultValue: null)`.
  Allowed exceptions: explicit `[Destructive]` attribute + a Status-log
  entry on the originating task (guardrail against accidents, not a hard
  no).
- **Dev-to-prod bridge CI test** (`tests/Nexora.Architecture.Tests/SchemaDriftTests.cs`,
  delivered with T-011): reads `ApplySchemaUpdatesAsync`'s statement array,
  extracts `(table, column)` pairs, and asserts each pair exists in the
  owning module's latest migration snapshot. Fails the release PR when a
  dev-only DDL would ship to production without a migration file.
- **Dev workflow unchanged**:
  1. Add DDL statement to `DevelopmentSeed.ApplySchemaUpdatesAsync`.
  2. Author the corresponding migration: `dotnet ef migrations add
     <Name> --project src/Modules/Nexora.Modules.X --context XDbContext
     --output-dir Migrations`.
  3. CI runs both tests; PR green or red tells you whether the bridge is
     healthy.
- **Rollback policy**: forward-only. A production migration that needs
  undoing is a new additive migration (`Remove<ColumnName>` writes a
  `DropColumn` — permitted with `[Destructive]` + explicit task reference).
  Step-back migrations are blocked by the additive-only test because
  step-back in a schema-per-tenant world is operationally ambiguous (some
  tenants advance, others stay).
- **Observability**:
  - Metric `nexora_migrations_applied_total{module,tenant}` counter on each
    successful migration.
  - Metric `nexora_migrations_duration_seconds{module}` histogram.
  - Log `Information` at migration start + end with tenant id, module, and
    the migration id string. Failures log at `Error` with the migration id
    and the SQL that threw.
- **Docs updates on Accept**:
  - `docs/standards/schema-migration.md` §0 banner changed from "Production
    strategy — TBD" to "Production via ADR-0027".
  - `docs/standards/schema-migration.md` §6 final bullet retargeted from
    the archived legacy doc to this ADR.
  - `CLAUDE.md` Database section loses its TBD paragraph.
  - `docs/operations/migration-orchestration.md` adds a top-of-doc
    "Derives from: ADR-0027" line next to ADR-0002 / ADR-0003.

## References

- ADR-0001 — Modular Monolith Architecture.
- ADR-0002 — Schema-per-Tenant Multi-Tenancy.
- ADR-0003 — Deployment Strategy (additive-only promise).
- `docs/standards/schema-migration.md` — dev-side standard this ADR
  supplements.
- `docs/operations/migration-orchestration.md` — operational runbook this
  ADR formalises.
- T-011 — MigrationRunner implementation (blocked on this ADR).
- T-021 — `HasFilter` drift fix (cited as the prototype of the drift class
  that the CI gate prevents).
- `docs/code-reviews/2026-03-29-comprehensive-opus.md` §"Issue: Raw SQL
  ALTER TABLE statements bypass EF Core migrations system" — the original
  flag.
- External: Metasfresh 10k-migration sprawl — cited in
  `docs/code-reviews/metasfresh-analiz-20260330.md` §"Şema Migrasyonu:
  Flyway" as the counter-example this decision is aware of.
