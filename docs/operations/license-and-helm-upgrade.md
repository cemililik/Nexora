# License Lifecycle & Helm Upgrade Runbook

**Derives from:** ADR-0003 (Deployment Strategy), ADR-0023 (NMP Billing Model).

Operational guide for license issuance, rotation, grace periods, and Helm chart upgrades across Cloud SaaS / Dedicated / Self-Hosted deployment modes.

## 1. License key format

RSA-2048 signed JWT. Header: `alg: RS256, kid: nexora-license-v1`. Payload:

```json
{
  "sub": "tenant-or-org-id",
  "iss": "https://license.nexora.io",
  "iat": 1714000000,
  "exp": 1745536000,
  "plan": "enterprise-20-seats",
  "modules": ["identity", "contacts", "crm", "finance"],
  "seat_limit": 20,
  "environment": "self-hosted",
  "features": { "sso": true, "audit_export": true },
  "grace_days_after_expiry": 14
}
```

Public key pinned in `appsettings.json` under `License:VerificationKeys`; rotated via config push.

## 2. License issuance

### 2.1 Cloud SaaS
- Licenses issued automatically by NMP on Stripe/iyzico subscription creation (ADR-0023).
- Platform reads entitlement from local `entitlements` table; no external license key needed.
- `/internal/license/verify` returns O(1) entitlement lookup from Redis (15-min TTL).

### 2.2 Dedicated
- Semi-automated: Ops issues license via NMP admin UI → NMP signs with platform private key (stored in HashiCorp Vault, key rotation annual) → emails .lic file to customer → customer uploads via admin portal.
- License applies to single tenant in the dedicated cluster.

### 2.3 Self-Hosted
- Fully manual: Sales → NMP issuance UI → .lic file delivered via secure email/customer portal.
- Customer uploads during first-run setup wizard.
- License file stored at `/var/lib/nexora/license.lic` (mounted as K8s secret).

## 3. License validation and grace period

### 3.1 Runtime validation
`LicenseService.ValidateAsync` runs:
- On platform startup (fails startup if license invalid/expired beyond grace).
- On every 6 hours via `platform:license-validation` Hangfire recurring job.
- On every `/internal/license/verify` call (cached 15 min).

### 3.2 Grace period (expiry handling)
- **Day 0 (expiry):** platform continues normally; warning banner appears in admin UI.
- **Day 1-14 (grace):** warning banner escalates; email alerts to tenant admin daily; `license.status = "expired_grace"`.
- **Day 15+ (hard expiry):** platform enters read-only mode — all write endpoints return `HTTP 402 Payment Required` with `lockey_license_expired` error. Read endpoints still serve (avoids data lock-out). NMP-managed cloud tenants are auto-suspended (no read access) per ADR-0023.

Configurable per deployment mode:
- Cloud SaaS: `grace_days_after_expiry = 7` (shorter — NMP manages billing).
- Dedicated: 14 days (default).
- Self-Hosted: 30 days (longer — manual renewal cycle).

### 3.3 Air-gapped / offline validation
Self-hosted deployments may have no internet access. License validation is fully offline (RSA signature verification only). No phone-home required for validation.

Optional phone-home (ADR-0023): daily heartbeat to `https://license.nexora.io/heartbeat` if reachable. Missing heartbeat does NOT trigger expiry; only informational telemetry.

## 4. License rotation

### 4.1 Renewal
New .lic file issued before expiry → customer uploads → old file replaced → `LicenseService.ReloadAsync` fires on file change (filesystem watcher) → new license takes effect with no restart.

### 4.2 Seat/module change
Mid-term upgrades (add seats, enable module) generate a new license with same `sub` but updated `plan` / `modules` / `seat_limit`. Customer uploads; hot reload.

### 4.3 Revocation
Revocation list published at `https://license.nexora.io/revocations.json` (signed). Platform fetches daily if online. Offline deployments receive a signed revocation bundle on next license renewal — no mid-term revocation for true air-gapped customers (policy limitation).

## 5. Helm chart versioning

### 5.1 Chart semver policy
- **Major (N.0.0):** breaking changes to values schema OR breaking Kubernetes resource restructure (e.g., new CRDs, StatefulSet → Deployment rewrite).
- **Minor (N.M.0):** new optional values, new sub-charts, non-breaking resource additions.
- **Patch (N.M.P):** bug fixes, chart-only fixes, dependency patch bumps.

Platform app version is tracked separately (`appVersion` in Chart.yaml). A single chart major may support multiple app versions within its support window.

### 5.2 Support window
Per ADR-0003 N-2: the current major + 2 previous majors of the chart are supported with security patches. Older majors receive no updates.

### 5.3 Breaking-change migration
Each chart major release ships a migration guide at `charts/nexora/UPGRADING-N.md` listing:
- Values.yaml schema changes (added, removed, renamed keys)
- Kubernetes resource changes (any resources that must be deleted before upgrade)
- Database migration coupling (which app version must run first)
- Estimated downtime (usually zero — rolling update)

## 6. Helm upgrade workflow

### 6.1 Minor / patch upgrade (zero downtime)
```bash
helm repo update nexora
helm upgrade nexora nexora/nexora \
  --namespace nexora \
  --values values.yaml \
  --version X.Y.Z \
  --atomic --timeout 10m
```
- Rolling deployment strategy; pod-by-pod replacement.
- Tenant migrations trigger automatically post-deploy (see `migration-orchestration.md`).
- `--atomic` flag ensures rollback on failure.

### 6.2 Major upgrade (with schema / CRD changes)
1. Read the `UPGRADING-N.md` guide.
2. Backup PostgreSQL (pg_dump or managed snapshot).
3. If CRDs changed: apply new CRDs manually (Helm does not upgrade CRDs):
   ```bash
   kubectl apply -f https://github.com/nexora/helm-charts/releases/download/vN.0.0/crds.yaml
   ```
4. Upgrade Helm release:
   ```bash
   helm upgrade nexora nexora/nexora \
     --namespace nexora \
     --values values.yaml \
     --version N.0.0 \
     --atomic --timeout 20m
   ```
5. Monitor tenant migration progress via `platform:migrate-tenants` job logs.
6. Verify health: `curl -f https://nexora.internal/health/ready` returns 200 per pod.

### 6.3 Rollback
For minor/patch: `helm rollback nexora N` restores the previous release.

For major upgrades with DB schema changes: rollback is NOT automatic. Recovery path:
1. Stop the new platform pods (`helm uninstall` or scale to 0).
2. Restore PostgreSQL from pre-upgrade backup.
3. Re-deploy the previous chart major.
4. Data written during the failed upgrade window is lost (document this risk in `UPGRADING-N.md`).

### 6.4 Canary / staged rollout
For multi-cluster deployments, use Flux/ArgoCD with progressive delivery:
- Canary 10% of traffic to new version; monitor SLO for 30 min.
- 50% rollout; monitor for 2h.
- 100% rollout.
- Cloud SaaS: use feature flags (`LaunchDarkly` or Nexora native `ITenantConfiguration`) to gate new functionality per tenant.

## 7. Common operational scenarios

### 7.1 Customer renews license mid-cycle
→ Section 4.1. Hot reload; no restart.

### 7.2 Customer exceeds seat limit
→ `LicenseService` returns `seat_limit_exceeded` warning; admin UI shows banner; **no enforcement block** on existing users. New user provisioning fails with `lockey_license_seat_limit_exceeded` until license upgraded or users removed.

### 7.3 Chart upgrade fails at K8s level
→ `--atomic` auto-rollback; logs go to `kubectl rollout status deployment/nexora-host`. Troubleshoot via `kubectl describe pod` + `kubectl logs`.

### 7.4 Tenant on older app version after cluster upgrade
→ `platform:migrate-tenants` should catch; if drift persists > 2h, see `migration-orchestration.md` §2.3.

## 8. References

- ADR-0003 — Deployment Strategy
- ADR-0023 — NMP Billing Model (license issuance details)
- `docs/operations/migration-orchestration.md`
- CLAUDE.md §Configuration — 5-layer config hierarchy

## 9. Task references

- T-014 (Phase 2 Milestone B) — `LicenseService.ValidateAsync` hot-reload watcher
- T-015 (Phase 2 Milestone B) — Revocation list fetcher
- T-016 (Phase 3) — Progressive rollout integration (LaunchDarkly evaluation)
