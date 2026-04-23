# Commit & Branch Style

Derives from: feedback rule (commit to `development` only, never `main` / `test`).
Source: legacy `RELEASE_STANDARDS.md` §1–7.

## 1. Conventional Commits

All commit messages MUST follow [Conventional Commits 1.0](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short summary>

<optional body>

<optional trailers>
```

### 1.1 Types

| Type | Meaning | Triggers release |
|------|---------|------------------|
| `feat` | New user-visible feature | MINOR |
| `fix` | Bug fix | PATCH |
| `refactor` | Code change without behavior change | none |
| `perf` | Performance improvement | PATCH |
| `test` | Add/adjust tests | none |
| `docs` | Documentation only | none |
| `chore` | Build, tooling, deps | none |
| `style` | Formatting, whitespace | none |
| `ci` | CI/CD changes | none |
| `build` | Build system | none |

Breaking changes use `!` after the type/scope **and** a `BREAKING CHANGE:` footer —
triggers MAJOR.

### 1.2 Scope = Module Name

Scope MUST be the module (or area) touched, lowercase kebab:

```
feat(crm): add lead pipeline stage management
fix(donations): correct multi-currency rounding
refactor(contacts): extract address value object
test(sponsorship): add installment payment tests
docs(api): update donation endpoint documentation
chore(deps): update EF Core to 10.0.1
feat(identity)!: remove deprecated /legacy-login endpoint

BREAKING CHANGE: /legacy-login removed; clients must use /api/v1/identity/login.
```

Multi-module changes: pick the primary module as scope and list the others in the body. If
the change is genuinely cross-cutting, use `chore(*)` or the most specific shared scope
(`shared-kernel`, `infrastructure`).

## 2. Mandatory Trailers

Every commit body (not just the subject) includes these trailers when relevant:

| Trailer | When | Example |
|---------|------|---------|
| `Lockey:` | Any commit that adds/changes a `lockey_` key | `Lockey: lockey_crm_lead_created_success (en, tr)` |
| `Module:` | Any commit touching `src/Modules/Nexora.Modules.*` | `Module: Nexora.Modules.CRM` |
| `ADR:` | Any commit that implements or derives from an ADR | `ADR: ADR-014` |
| `Co-Authored-By:` | Pair programming or AI-assisted | standard git trailer |

`Lockey:` trailers make localization audits trivial — a `git log --grep 'Lockey:'` lists
every translation key touched.

## 3. Branch Flow

### 3.1 Nexora Branch Model

```
main          ← production (protected)
test          ← staging (protected)
development   ← active trunk (all feature merges target this)
  ├── feature/NEX-123-lead-pipeline
  ├── bugfix/NEX-456-donation-rounding
  ├── hotfix/NEX-789-payment-timeout
  ├── chore/NEX-101-update-deps
  └── docs/NEX-202-api-docs
```

**Rules (enforced by review and by repo settings):**

- **All feature and fix branches merge to `development` only.**
- **Never commit directly to `main` or `test`.** These branches are advanced by a
  release-engineering workflow that promotes `development` after acceptance.
- Hotfix exception: a hotfix may branch from `main`, but the PR still merges into
  `development` first; a separate promotion cherry-picks to `main`. No exceptions without an
  explicit maintainer approval in the PR.
- Feature branches are short-lived (< 1 week target).

### 3.2 Branch Naming

```
feature/NEX-<issue>-<short-slug>
bugfix/NEX-<issue>-<short-slug>
hotfix/NEX-<issue>-<short-slug>
chore/NEX-<issue>-<short-slug>
docs/NEX-<issue>-<short-slug>
```

Issue key is required — no detached branches without a tracked task.

## 4. Pull Request Requirements

- Linked to a task / issue.
- All CI checks green (build, lint, markdownlint, tests, architecture tests).
- At least 1 approval (2 for security-sensitive modules — see
  [security-review.md](security-review.md)).
- No unresolved review conversations.
- PR size target: < 400 changed lines (excluding generated files). Split larger work.
- PR description uses the repo template: **Summary**, **Test plan**, **Breaking changes**,
  **ADRs referenced**.

## 5. Squash Merge Policy

- **Squash merge** into `development`. No merge commits, no rebase-and-merge on the trunk.
- The squash commit MUST itself conform to §1 (type, scope, subject).
- The squash commit body preserves the mandatory trailers from §2 — copy them out of the
  constituent commits before merging.
- Never use `--no-verify` or `--force` on protected branches.
- Hooks and signing are never skipped without explicit maintainer approval.

## 6. Release Flow

Full release standards in legacy `RELEASE_STANDARDS.md` §1, §3, §6, §7. Summary:

- Semantic Versioning (`MAJOR.MINOR.PATCH`); pre-releases `-alpha.N`, `-beta.N`, `-rc.N`.
- Each module carries its own version independent of platform.
- Release pipeline is triggered by a `v*` tag on `main`, after `development → test → main`
  promotion.
- `CHANGELOG.md` updated on every user-visible change (see [DOC-7] in
  [code-review.md](code-review.md)).
- Migrations are additive in production (no `DROP` / column rename without expand-contract).

## 7. Commit Examples

```
feat(donations): add recurring donation checkout flow

Adds CheckoutRecurringCommand + Stripe SetupIntent integration.
Outbox entry emitted on success.

Lockey: lockey_donations_recurring_created (en, tr)
Module: Nexora.Modules.Donations
ADR: ADR-014
```

```
fix(identity): prevent duplicate email on soft-deleted user

Unique index now has HasFilter("IsDeleted = false").

Module: Nexora.Modules.Identity
```

```
docs(standards): add permissions matrix template

ADR: ADR-004, ADR-006
```
