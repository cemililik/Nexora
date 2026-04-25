# Phase 2 — Tasks

Task files for Phase 2 (Enterprise Core). Phase 2 has not started executing
yet — see [`../../../roadmap/current.md`](../../../roadmap/current.md) — but the
task stubs and scoped slices below are pre-allocated so the dependency
ADRs (0026–0031) and the carry-overs from Phase 1.5 have explicit homes.

For Phase 2's strategic scope, exit bar, and milestones, see
[`../../../roadmap/phases/phase-2-enterprise.md`](../../../roadmap/phases/phase-2-enterprise.md).

## Currently filed tasks

| Task | Title | Milestone | Status | Notes |
|------|-------|-----------|--------|-------|
| [T-010](T-010.md) | Cross-module audit payload PII scan for GDPR erasure | A | Not started | Reclassified from Phase 1.5 (carry-over sweep 2026-04-24); scaffolding ships alongside the CRM Portal Extension pilot. Unblocked by [ADR-0026](../../../decisions/0026-cross-module-pii-payload-scan-for-gdpr-erasure.md). |
| [T-011](T-011.md) | `MigrationRunner.MigrateAllModulesAsync` implementation | A | Not started | Unblocked by [ADR-0027](../../../decisions/0027-production-schema-migration-strategy.md). |
| [T-012](T-012.md) | `AdditiveOnlyMigrationTest` architecture test | A | Not started | Pairs with T-011 — CI gate for the additive-only rule. |
| [T-013](T-013.md) | `platform:audit-migration-drift` Hangfire job | B | Not started | Nightly drift audit for tenant migration heads. |
| [T-013a](T-013a.md) | Platform outbox — `outbox_messages_platform` + OutboxProcessor scope | B | Not started | T-013 follow-up; closes the AC4 "via outbox" deviation. |
| [T-014](T-014.md) | `LicenseService.ValidateAsync` hot-reload watcher | B | Not started | Unblocked by [ADR-0030](../../../decisions/0030-license-hot-reload-mechanism.md). |
| [T-015](T-015.md) | License revocation list fetcher | B | Not started | Daily signed-bundle fetch with offline fallback. |
| [T-016](T-016.md) | Progressive rollout / feature-flag service | C | Not started | `IFeatureFlagService` abstraction + tenant-cohort toggles. |
| [T-025](T-025.md) | `platform:purge-uninstalled-modules` Hangfire cleanup job | A | Not started | Unblocked by [ADR-0028](../../../decisions/0028-module-uninstall-data-retention-contract.md) (retention contract). |
| [T-026](T-026.md) | Cascade guard in `UninstallModuleCommand` | A | Not started | Unblocked by [ADR-0028](../../../decisions/0028-module-uninstall-data-retention-contract.md) + [ADR-0031](../../../decisions/0031-cascade-uninstall-per-module-transactions.md) (cascade transaction policy). |
| [T-027](T-027.md) | GDPR erasure escape hatch for renamed uninstall tables | A | Not started | Module-local scan; ADR-0028 §Implementation notes. |
| [T-030](T-030.md) | Portal Extension end-to-end pilot — CRM module | A | Not started | Carries Phase 1.5.4 deferral; first real consumer of [ADR-0017](../../../decisions/0017-portal-extension-architecture.md). |

Per-Phase-2-module demo-data seed tasks (the "T-007b-*" slices from the
T-007 split) are NOT pre-allocated — they are filed at each owning module's
kickoff (CRM, Finance, Subscription, Projects) so the seed content lands
together with the module instead of as orphan placeholders. The registry
they target ships in [T-029](../phase-1.5/T-029.md) (still in the Phase 1.5
folder because that is where its acceptance criteria can be satisfied
without Phase 2 modules existing yet).

## Conventions

- One markdown file per task, naming `T-NNN.md` (3-digit zero-padded,
  globally unique across the repo).
- Status vocabulary and creation rules: [`../README.md`](../README.md).
- Phase-2 task stubs may carry minimal acceptance criteria at file-creation
  time; AC are expanded as each milestone enters execution. Stubs exist so
  the ADRs and carry-overs that name them have a stable target file.

## Related

- [Phase 2 plan](../../../roadmap/phases/phase-2-enterprise.md) — exit bar, milestones, ADR ledger.
- [Phase 1.5 tasks](../phase-1.5/) — predecessor tasks that the Phase 2 stubs build on.
- [Tasks index + status vocabulary](../README.md).
