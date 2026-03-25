# E2E & Regression Test Scenarios — Phase 0 & Phase 1

## Execution Order

Tests MUST be executed in the following order. Each suite depends on the previous ones completing successfully.

| Order | File | Suite | Scenarios | Prerequisite |
|-------|------|-------|-----------|--------------|
| 1 | [01-INFRASTRUCTURE.md](01-INFRASTRUCTURE.md) | Phase 0 — Infrastructure | 18 | Docker Compose running |
| 2 | [02-IDENTITY.md](02-IDENTITY.md) | Identity & Access Management | 32 | Suite 01 passed |
| 3 | [03-CONTACTS.md](03-CONTACTS.md) | Contact Management | 35 | Suite 02 passed |
| 4 | [04-NOTIFICATIONS.md](04-NOTIFICATIONS.md) | Notification Engine | 22 | Suite 03 passed |
| 5 | [05-DOCUMENTS.md](05-DOCUMENTS.md) | Document Management | 28 | Suite 03 passed |
| 6 | [06-REPORTING.md](06-REPORTING.md) | Reporting Engine | 24 | Suite 03 passed |
| 7 | [07-ADMIN-DASHBOARD.md](07-ADMIN-DASHBOARD.md) | Admin Dashboard (UI) | 30 | Suite 02–06 passed |
| 8 | [08-PORTAL.md](08-PORTAL.md) | Portal Framework (UI) | 14 | Suite 02 passed |
| 9 | [09-CROSS-MODULE.md](09-CROSS-MODULE.md) | Cross-Module & Regression | 20 | All suites passed |

**Total: 223 test scenarios**

## Environment Setup

Before running tests, ensure:

```bash
# 1. Start all infrastructure
cd Nexora && docker compose up -d

# 2. Wait for all services to be healthy
docker ps --format "{{.Names}} {{.Status}}" | grep nexora

# 3. Start frontend dev servers
cd src/Clients/nexora-admin && npm run dev &   # :3001
cd src/Clients/nexora-portal && npm run dev &  # :3000
```

## Test Credentials

| User | Email | Password | Role |
|------|-------|----------|------|
| Platform Admin | `platformadmin@nexora.dev` | `Admin123!` | Platform Admin (all permissions) |

## Service URLs

| Service | URL |
|---------|-----|
| API (via APISIX) | `http://localhost:9080/api/v1` |
| API (direct) | `http://localhost:5100/api/v1` |
| Admin Dashboard | `http://localhost:3001` |
| Portal | `http://localhost:3000` |
| Keycloak Admin | `http://localhost:8080` |
| MinIO Console | `http://localhost:9001` |
| Grafana | `http://localhost:3300` |
| pgAdmin | `http://localhost:5051` |

## Status Legend

- [ ] Not tested
- [x] Passed
- [!] Failed — needs investigation
