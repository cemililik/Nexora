# CI opt-in tests

**Derives from:** T-024 (schema-drift wrapper) and the test categorisation
convention introduced with the 6-task review-independent batch.

This runbook documents tests that ship in the codebase but are **skipped by
default** in the PR CI pipeline. Each one runs only when an explicit
environment flag is set, and operators choose where (and when) to enable
them.

The pattern keeps PR feedback fast — a Postgres + Python + docker-compose
gate adds minutes to every PR run for a check that catches a slow-moving
class of drift — while still surfacing failures in nightly / pre-release
jobs that have the dependencies wired up.

## At a glance

| Test | Trait | Env flag | Default | Required infra |
|------|-------|----------|---------|----------------|
| [`SchemaDriftToolTests`](../../tests/Nexora.Infrastructure.Tests/Tooling/SchemaDriftToolTests.cs) | `Category=Tooling` | `NEXORA_SCHEMA_DRIFT_ENABLED=1` | **Skipped** | docker-compose (Postgres) + Python 3.11 + `tools/check-schema-drift.py` |
| (Identity slow-query) `Handle_SlowQuery_LogsWarning` | n/a | n/a (xUnit `[Skip]`) | **Skipped** | n/a — historical opt-in for a slow path; no env flag |

## SchemaDriftToolTests

### What it does

Wraps [`tools/check-schema-drift.py`](../../tools/check-schema-drift.py) in
an xUnit `[SkippableFact]`. When enabled, the test shells out to
`python3 tools/check-schema-drift.py`, captures stdout / stderr, and
fails when the script exits non-zero. The script compares every EF
entity's mapped properties against the live dev tenant schema.

### When to enable

- **Nightly CI**: yes. Schema drift is the exact class of bug T-021 / T-022
  exists to prevent; nightly catches it before a PR run does.
- **Pre-release CI**: yes. The release-cut bridge gate from ADR-0027
  reads schema state, so drift here implies dev-to-prod drift later.
- **Per-PR CI**: no by default. Adds Postgres + Python startup to every
  PR for a slow-moving signal. Opt in only on PRs that touch
  `src/Nexora.Host/DevelopmentSeed.cs` or any `Modules/*/Infrastructure/Configurations/*.cs`.

### How to enable in CI

#### GitHub Actions

```yaml
jobs:
  schema-drift-nightly:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:17-alpine
        env:
          POSTGRES_USER: nexora
          POSTGRES_PASSWORD: nexora
          POSTGRES_DB: nexora
        options: --health-cmd "pg_isready -U nexora" --health-interval 5s
        ports: ['5432:5432']
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }
      - uses: actions/setup-python@v5
        with: { python-version: '3.11' }
      - name: Apply DevelopmentSeed (boots schema)
        run: docker compose up -d nexora-api && sleep 20
      - name: Run drift wrapper
        env:
          NEXORA_SCHEMA_DRIFT_ENABLED: '1'
        run: dotnet test --filter "Category=Tooling"
```

#### Local

```bash
docker compose up -d
NEXORA_SCHEMA_DRIFT_ENABLED=1 dotnet test \
  tests/Nexora.Infrastructure.Tests/Nexora.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~SchemaDriftToolTests"
```

### Reading the result

Test runners surface the wrapper one of three ways:

| Outcome | What it means | Operator action |
|---------|--------------|-----------------|
| **Skipped** | Env flag not set OR `python3` / script missing. Default state on PR CI. | None — by design. |
| **Passed** | The drift detector reported zero drift against the live dev schema. | None. |
| **Failed** | The script's full stdout + stderr are in the failure message. The first non-passing line names the offending entity / column. | Fix the drift in either `DevelopmentSeed.ApplySchemaUpdatesAsync` or the EF model so the two converge, then re-run. |

### Observability hooks

`xunit.runner.visualstudio` reports `Skipped` distinctly from `Passed` /
`Failed` in TRX output, so a CI dashboard that consumes `Skipped` counts
will see this test as 1 skip per run when the env flag is unset. Most
dashboards (Azure DevOps, GitHub Actions test summary, ReportPortal) treat
skipped as informational and do not gate merges on it; **do not** wire a
gate that fails when this test is skipped — that defeats the opt-in
pattern.

If you need a hard signal that the nightly drift run actually executed,
assert on the test count being non-zero in the nightly job (e.g.
`grep -E "^Passed!.*Tooling" trx.log`) rather than on Skipped count.

### Updating this runbook

When a new opt-in test ships, add a row to the at-a-glance table and a
section describing what / when / how. Keep this doc the single source of
truth for "tests we deliberately skip in PR CI" so a new engineer can
answer the question without spelunking the code.
