# Performance Reviews

Load tests, profiling sessions, SLO-review snapshots. Each review is dated evidence, not
general guidance.

## File naming

`YYYY-MM-DD-<scope-slug>.md` — e.g. `2026-07-01-outbox-processor-load.md`.

## Minimum sections

- **Context** — what system slice, which environment, what test harness.
- **Methodology** — load profile, duration, instrumentation.
- **Results** — latency percentiles, throughput, error rate, resource usage.
- **Findings** — numbered (Blocker / Major / Minor).
- **Follow-up** — tasks opened, standards updated, ADRs proposed.

## Related

- [Reviews root](../README.md)
- `docs/standards/OBSERVABILITY_STANDARDS.md` — OpenTelemetry + metrics conventions
- Part of Phase 4 §4.7 cross-phase continuous workstream (performance / load testing)
