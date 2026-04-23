---
name: pre-commit-verify
description: Run all required pre-commit checks — frontend lint, backend build, architecture tests, unit tests — before committing. Use when the user asks to "commit", "prepare for PR", "run checks", or has completed a feature and is about to stage changes.
---

# Pre-Commit Verify

Matches the CI pipeline's gate. **Do NOT commit if any step fails.**

## Branch rule
Commits go to `development` only — never `main` or `test` directly. Confirm current branch with `git branch --show-current` before committing.

## 1. Frontend lint (mandatory for any Clients/ change)
```bash
# If nexora-admin changed:
cd src/Clients/nexora-admin && npm run lint

# If nexora-portal changed:
cd src/Clients/nexora-portal && npm run lint
```
Any lint error blocks the commit. Fix, don't suppress with `eslint-disable` unless justified.

## 2. Frontend typecheck + tests (if Clients/ changed)
```bash
npm run typecheck   # tsc --noEmit
npm test -- --run   # vitest
```

## 3. Backend build (if src/Modules or src/Nexora.* changed)
```bash
dotnet build Nexora.sln -c Release
```
Zero warnings policy if enabled in Directory.Build.props.

## 4. Backend tests
```bash
dotnet test Nexora.sln --no-build -c Release
```
Includes architecture tests that enforce module boundaries — these MUST pass.

## 5. Localization audit (if user-facing strings touched)
- Every new `lockey_` key exists in `en` + `tr` JSON files.
- `grep -r "lockey_new_key_here"` appears on backend + both locale files.
- No hardcoded user-facing strings in handlers / validators / JSX.

## 6. Commit message
Conventional Commits only:
```
<type>(<scope>): <subject>

[optional body]
```
Types: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`, `perf`, `build`, `ci`.
Scope usually = module name (`contacts`, `identity`, `admin-ui`).

## 7. Final checks
- No secrets (.env, keys, tokens) staged — re-inspect `git diff --cached`.
- No `console.log`, no `TODO` comments blocking review.
- No `catch (Exception)` added outside `GlobalExceptionHandler` / `NexoraJob`.

If all pass → commit. Ask the user before pushing.
