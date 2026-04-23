# Archive

This directory preserves superseded or deferred documentation that the project has stopped
maintaining but is not ready (or permitted) to delete.

## What lives here

- **Superseded roadmap.** The legacy monolithic `docs/roadmap/ROADMAP.md` was split into
  per-phase files under `docs/roadmap/phases/` (see ADR-015). A verbatim copy lives here as
  `roadmap-legacy.md`.
- **Legacy implementation plans** (`roadmap-legacy-plans/`). Once-active plan documents whose
  subject has shipped (outbox/inbox pattern, soft-delete migration). Retained for historical
  context.
- **Tier-4 / Phase-deferred module specs** (`specs-legacy/`). Module SPEC files for modules that
  are deferred to Phase 4 or later (POS, Fleet, Inventory, Surveys, CMS). They are not being
  revised and should not be cited as current product direction.
- **Migration notes** (`migration-notes.md`). Phase 1 inventory and the Phase 2 execution log.

## Rules

1. **Content is preserved verbatim.** Files here are snapshots. We do not edit them to "keep
   them current"; if they drift they are still useful as history.
2. **No new work happens in this directory.** New specs, plans, and ADRs go to their live
   locations. The only legitimate writes to `_archive/` are:
   - appending to `migration-notes.md`'s execution log,
   - adding a new archived file (copy from source),
   - deleting a file (maintainer-only).
3. **Adding to the archive.** Use `cp` — never `mv` — so the original remains in place until a
   maintainer explicitly decides to delete it. Add a 3–5 line header stating the archive date,
   the original path, and the replacement location.
4. **Removing from the archive.** Maintainer task only. A PR that deletes an archived file
   must cite the ADR or issue authorising the deletion.

## Related

- **ADR-015** — Roadmap Structure (reason the legacy roadmap was retired).
- **Standard**: `../standards/documentation-style.md` (or the legacy
  `DOCUMENTATION_STANDARDS.md` until the split lands).
- `../roadmap/phases/` — live roadmap.
