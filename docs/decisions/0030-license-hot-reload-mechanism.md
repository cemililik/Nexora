# 0030 — License hot-reload mechanism (filesystem watcher vs. K8s secret-volume signal)

## Status

Accepted  <!-- Proposed | Accepted | Superseded by NNNN -->

## Date

2026-04-24

## Context

T-014 implements hot-reload for the on-prem license file so a customer
uploading a new `.lic` (via the admin portal or by writing to the secret
mount path) takes effect without restarting pods. The implementation task
explicitly defers the mechanism choice to this ADR — it is an architecture
decision, not an implementation detail.

Two realistic options:

1. **`FileSystemWatcher`** (the .NET `FileSystemWatcher` class on a fixed
   path, e.g. `/var/lib/nexora/license.lic`).
2. **Kubernetes secret-volume reload signal** — rely on the kubelet's
   atomic-swap behaviour when a Secret is updated. The mounted file path
   stays the same; the underlying inode flips. No file-watcher primitive
   from the app's side — instead, a short polling loop (or an explicit
   `SIGHUP` + handler) re-reads the file on a schedule.

The two options produce the same runtime outcome (new license picked up
without a restart) but differ sharply on platform semantics, operational
ergonomics, and failure modes. This ADR records the choice so T-014 ships
against one of them instead of re-litigating.

## Decision drivers

- **Kubernetes is the primary prod target** per ADR-0003 (deployment
  strategy). Self-hosted bare-metal is a supported secondary target but not
  the design centre.
- **`FileSystemWatcher` is unreliable on mounted volumes.** On Linux the
  backing is `inotify`, which does not fire events for container volume
  types that use overlay / fuse / projected filesystems. Kubernetes
  projected Secret volumes (the default for `kubectl create secret`
  mounted as a file) are one of the affected filesystems: the inode swap
  under the mount point is invisible to inotify watchers rooted above the
  swap point. We'd silently stop hot-reloading under the exact deployment
  model the feature is supposed to serve.
- **Bare-metal still needs to work.** On a plain ext4 mount, inotify fires
  reliably. A solution that only works on Kubernetes loses the on-prem
  audience.
- **Operator visibility.** When a reload fails (invalid signature, corrupt
  JSON, expired license), the admin needs an event they can see on a
  dashboard + alert off of. Event emission must not depend on whether an OS
  signal was delivered.
- **Testability.** Unit tests should not require a real inotify kernel or
  a running K8s sidecar.

## Considered options

### 1. Pure `FileSystemWatcher`

Native .NET `FileSystemWatcher` rooted at the license directory; on
`Changed` / `Created`, invoke `LicenseService.ValidateAsync`.

- Pros
  - Event-driven, zero polling overhead.
  - Works on bare-metal Linux + Windows out of the box.
- Cons
  - Silently no-ops on K8s projected Secret volumes (inotify does not fire
    for atomic symlink swap under the mount point). This is the dominant
    prod deployment model; the feature would be broken in production.
  - Test doubles (vfs / in-memory fs) produce flaky events on macOS.

### 2. Pure polling loop

Background `IHostedService` that reads the file every N seconds, hashes
the content, and re-validates when the hash changes.

- Pros
  - Uniform across Linux / K8s / Windows / macOS / bare-metal.
  - Trivially testable — inject a fake clock, control file contents.
  - Failure modes (unreadable file, expired license) naturally surface as
    log + event at every tick.
- Cons
  - Latency bounded by poll interval (30 s default is operator-tolerable
    for a license flip; not a 30 ms feature).
  - Small, constant background IO. Negligible for a single file.

### 3. Polling loop + `SIGHUP` short-circuit *(chosen)*

Same polling loop as Option 2 for the baseline correctness and uniformity,
PLUS a `SIGHUP` handler registered via `PosixSignalRegistration` that
interrupts the next tick and triggers an immediate reload. Operators who
want instant reload can `kubectl exec` + `kill -HUP 1` or wire the
admin-portal upload flow to send a signal.

- Pros
  - Inherits Option 2's uniformity + testability (polling is the
    baseline — `SIGHUP` is a bonus).
  - Instant reload on operator demand without depending on inotify +
    projected-volume semantics.
  - Graceful degradation: if `PosixSignalRegistration` is unavailable
    (Windows), the polling loop alone still works.
  - Observable: every tick emits a `license.checked` structured log and
    reload attempts emit `license.refreshed` / `license.refresh.failed`
    integration events regardless of how the reload was triggered.
- Cons
  - Two code paths to test (scheduled + signal-triggered). Mitigated by
    routing both through a single `PerformReloadAsync` method so the
    scheduler and signal handler share post-trigger behaviour.
  - `SIGHUP` on Linux is still an on-prem-only convenience today — K8s
    operators typically won't send signals. That's fine: they get the
    polling-baseline behaviour, which is what the pod has to support
    anyway.

## Decision outcome

Adopt Option 3: **polling loop + `SIGHUP` short-circuit**.

Implementation shape (load-bearing for T-014):

- `LicenseReloadService : IHostedService` — hosts the polling loop.
- `Polling interval` resolved via `IConfigurationResolver` under key
  `platform.license.reload_interval_seconds` (default 30, range 5–600).
- On each tick: compute SHA-256 of the file content; if unchanged, emit
  `license.checked` (Debug). If changed, call `LicenseService.ValidateAsync`
  on the new bytes; on success, swap the in-memory `ILicenseProvider`
  snapshot + emit `LicenseRefreshedIntegrationEvent`; on failure, keep
  the previous valid snapshot + emit `LicenseRefreshFailedIntegrationEvent`
  with the validation error and log at `Error`.
- `PosixSignalRegistration.Create(PosixSignal.SIGHUP, handler)` on
  startup when `OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()`;
  handler cancels the current polling sleep so the next loop iteration is
  the forced re-check.
- The in-memory snapshot is swapped via `Interlocked.Exchange` on an
  `ILicenseSnapshot`-shaped record — callers read through
  `Volatile.Read` so a mid-request reload never surfaces a partially-
  swapped snapshot.
- Integration test uses `FakeFileSystem` + `FakeTimeProvider` to drive
  the polling loop deterministically; a second integration test validates
  the signal path by directly invoking the private handler method via
  `InternalsVisibleTo` on the test assembly.

## Consequences

### Positive

- Feature ships against the prod deployment target (K8s projected Secret
  volumes) AND the on-prem target (bare-metal ext4) with one code path,
  instead of two mechanisms gated on the platform.
- Operator diagnostics are strong: every tick is observable, every reload
  attempt emits an event, and `SIGHUP` gives operators a no-latency
  escape hatch without needing inotify-level magic.
- T-014 WP1 can proceed — the mechanism is pinned.

### Negative

- 30-second default latency on K8s secret swaps. Documented in
  `docs/operations/license-and-helm-upgrade.md` §4.1 with the
  `platform.license.reload_interval_seconds` tuning knob + the
  `SIGHUP` escape hatch for operators that want immediate reload.
- A tiny, constant poll IO even when nothing changes. Negligible for a
  single file; not a hot path.

### Neutral

- No schema change. No new platform-level tables.
- `FileSystemWatcher` is NOT used anywhere in T-014 — if a future
  contributor wires it in thinking they need event-driven semantics,
  they'll silently break the K8s path. The T-014 AC#3 architecture test
  guards against any `using System.IO` + `FileSystemWatcher` reference
  under `Nexora.Infrastructure.Licensing.*` landing without an explicit
  `#pragma warning` suppression that cites this ADR.

## References

- ADR-0003 — Deployment strategy (K8s is primary).
- ADR-0023 — NMP billing model / provisioning gate (license contract).
- T-014 — `LicenseService.ValidateAsync` hot-reload watcher (the
  implementation task this ADR unblocks).
- `docs/operations/license-and-helm-upgrade.md` §4.1 — operational
  runbook entry that documents the 30 s default latency + `SIGHUP`
  escape hatch.
- .NET `PosixSignalRegistration` docs — cross-platform signal API used
  for the `SIGHUP` path (no-op on Windows).
