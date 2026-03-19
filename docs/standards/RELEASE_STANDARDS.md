# Nexora - Release & Deployment Standards

## 1. Versioning

### Semantic Versioning (SemVer)
```
MAJOR.MINOR.PATCH

1.0.0  → First stable release
1.1.0  → New feature (backward compatible)
1.1.1  → Bug fix
2.0.0  → Breaking change
```

### Pre-release Tags
```
1.0.0-alpha.1   → Internal testing
1.0.0-beta.1    → External beta testing
1.0.0-rc.1      → Release candidate
```

### Module Versioning
Each module has its own version independent of the platform:
```
Platform:        v1.2.0
CRM Module:      v1.1.0
Donations Module: v1.3.0
Sponsorship:     v1.0.0
```

## 2. Branching Strategy

### GitHub Flow (Simplified)
```
main (production-ready)
├── feature/NEX-123-lead-pipeline
├── feature/NEX-124-donation-cart
├── bugfix/NEX-125-amount-rounding
└── release/1.2.0 (cut from main when ready)
```

### Rules
- `main` is always deployable
- Feature branches are short-lived (< 1 week ideally)
- All changes go through PR → review → squash merge
- Release branches are for stabilization only (bug fixes, no new features)
- Hotfix: branch from `main`, fix, PR back to `main`

## 3. CI/CD Pipeline

### Pull Request Pipeline
```yaml
trigger: pull_request → main

steps:
  1. Restore & Build (.NET)
  2. Run Analyzers (Roslyn, StyleCop)
  3. Run Unit Tests
  4. Run Integration Tests (Testcontainers)
  5. Run Architecture Tests
  6. Code Coverage Check (fail if below threshold)
  7. SonarQube Analysis
  8. Build Docker Image (verify it builds)
  9. Frontend Lint & Test (if changed)
```

### Main Branch Pipeline
```yaml
trigger: push → main

steps:
  1. All PR pipeline steps
  2. Build & Push Docker Images (tagged: sha + latest)
  3. Deploy to Staging (automatic)
  4. Run E2E Tests against Staging
  5. Notify team (Slack/Teams)
```

### Release Pipeline
```yaml
trigger: tag v*

steps:
  1. All main pipeline steps
  2. Build & Push Docker Images (tagged: version)
  3. Generate Changelog
  4. Create GitHub Release
  5. Deploy to Production (manual approval gate)
  6. Run Smoke Tests
  7. Notify stakeholders
```

## 4. Environments

| Environment | Purpose | Deploy Trigger | Data |
|------------|---------|---------------|------|
| Local | Development | `docker compose up` | Seed data |
| CI | Automated tests | Every PR | Ephemeral (Testcontainers) |
| Staging | Pre-production validation | Auto on main merge | Anonymized prod copy |
| Production | Live system | Manual approval after tag | Real data |

## 5. Docker Standards

### Image Naming
```
ghcr.io/cemililik/nexora/api:1.2.0
ghcr.io/cemililik/nexora/api:latest
ghcr.io/cemililik/nexora/portal:1.2.0
ghcr.io/cemililik/nexora/admin:1.2.0
```

### Dockerfile Guidelines
- Multi-stage builds (build → publish → runtime)
- Use `.dockerignore` to minimize context
- Run as non-root user
- Use specific base image tags (not `latest`)
- Health check endpoints

### Docker Compose (Development)
```
docker compose up
```
Must bring up: PostgreSQL, Redis, Kafka, Keycloak, APISIX, MinIO, Vault, and the Nexora app.

## 6. Database Migration Strategy

- **Tool**: EF Core Migrations
- **Approach**: Code-first
- Migrations stored in `Infrastructure/Persistence/Migrations/`
- Each module owns its own migrations
- Migrations are **additive only** in production (no destructive changes)
- Breaking schema changes require a migration plan (expand-contract pattern)
- Migration naming: `YYYYMMDDHHMMSS_{Description}.cs`

### Tenant Migration
- New tenants get all migrations applied to their new schema
- Existing tenants get incremental migrations
- Migration status tracked per tenant

## 7. Release Checklist

```markdown
## Pre-Release
- [ ] All tests pass on main
- [ ] No critical/high SonarQube issues
- [ ] CHANGELOG.md updated
- [ ] API documentation generated and reviewed
- [ ] Database migration tested on staging
- [ ] Performance benchmarks within acceptable range
- [ ] Security scan clean

## Release
- [ ] Tag created (vX.Y.Z)
- [ ] Docker images built and pushed
- [ ] GitHub release created with changelog
- [ ] Staging deployment verified
- [ ] Production deployment approved
- [ ] Smoke tests pass on production

## Post-Release
- [ ] Monitor error rates for 1 hour
- [ ] Monitor performance dashboards
- [ ] Notify stakeholders of release
- [ ] Update project board / roadmap
```

## 8. Rollback Strategy

- **Blue-Green deployment**: Switch traffic back to previous version
- **Database**: Backward-compatible migrations mean old code works with new schema
- **Feature flags**: Disable features without redeployment
- **Rollback window**: 1 hour post-deployment, after which a hotfix is preferred
