# Suite 01 — Phase 0: Infrastructure & Foundation

**Prerequisite:** `docker compose up -d` completed successfully
**Execution Order:** 1 (first suite)

---

## 1.1 Docker Services Startup

- [ ] **TC-INF-001** — All 17 Docker containers start without errors
  - Run `docker compose up -d` and verify no exit codes > 0
  - Verify: `docker ps | grep nexora` shows 17 running containers

- [ ] **TC-INF-002** — All health checks pass
  - PostgreSQL: `docker exec nexora-postgres pg_isready` → success
  - Redis: `docker exec nexora-redis redis-cli ping` → PONG
  - Kafka: broker API versions endpoint responds
  - MinIO: `docker exec nexora-minio mc ready local` → ready
  - Keycloak: `http://localhost:8080` → Keycloak welcome page
  - Vault: `http://localhost:8200` → Vault UI
  - API: `http://localhost:5100/health` → 200 OK
  - APISIX: `http://localhost:9080` → responds

- [ ] **TC-INF-003** — Dev tools are accessible
  - pgAdmin: `http://localhost:5051` → login page
  - RedisInsight: `http://localhost:5541` → UI loads
  - Kafka UI: `http://localhost:8085` → shows nexora-dev cluster
  - MinIO Console: `http://localhost:9001` → Object Browser
  - Grafana: `http://localhost:3300` → dashboards page

## 1.2 Database & Multi-Tenancy

- [ ] **TC-INF-004** — Development seed runs successfully
  - API startup logs show `[DevSeed]` messages without errors
  - Dev tenant `00000000-0000-0000-0000-000000000001` exists in `platform.tenants`

- [ ] **TC-INF-005** — Tenant schema is created
  - Connect to PostgreSQL via pgAdmin
  - Verify schema `tenant_00000000_0000_0000_0000_000000000001` exists
  - Verify Identity, Contacts, Documents, Notifications, Reporting tables exist in schema

- [ ] **TC-INF-006** — Permission seed is complete
  - Query `identity_permissions` table → 63 permissions across 5 modules
  - Platform Admin role has all 63 permissions assigned

## 1.3 Keycloak Authentication

- [ ] **TC-INF-007** — Keycloak realm exists
  - `http://localhost:8080/realms/nexora-dev/.well-known/openid-configuration` → valid OIDC config
  - Issuer matches `http://localhost:8080/realms/nexora-dev`

- [ ] **TC-INF-008** — Keycloak clients exist
  - `nexora-admin` client (public, PKCE enabled)
  - `nexora-portal` client (confidential)
  - `nexora-gateway` client (confidential, for APISIX)

- [ ] **TC-INF-009** — Platform admin user exists in Keycloak
  - User `platformadmin@nexora.dev` exists in `nexora-dev` realm
  - User has `tenant_id` and `organization_id` custom attributes
  - User can obtain token via admin console login

## 1.4 APISIX API Gateway

- [ ] **TC-INF-010** — JWT validation works
  - Request without token to `/api/v1/identity/users/me` → 401
  - Request with valid token → 200 with user data

- [ ] **TC-INF-011** — CORS headers present
  - OPTIONS preflight from `http://localhost:3001` → CORS headers returned
  - OPTIONS preflight from `http://localhost:3000` → CORS headers returned
  - Request from unknown origin → no CORS headers

- [ ] **TC-INF-012** — Rate limiting works
  - Send 200 rapid requests to `/api/v1/identity/users/me`
  - Verify: after burst limit, 429 Too Many Requests returned

- [ ] **TC-INF-013** — Correlation ID propagated
  - Send request to any API endpoint
  - Verify: response contains `X-Correlation-Id` header
  - Verify: same ID appears in API logs (Grafana → Loki)

## 1.5 Secrets Management

- [ ] **TC-INF-014** — Vault secrets are seeded
  - `vault kv get secret/nexora/postgres` → connection string
  - `vault kv get secret/nexora/minio` → endpoint, access-key, secret-key
  - `vault kv get secret/nexora/keycloak` → base-url, admin credentials
  - `vault kv get secret/nexora/redis` → connection string

- [ ] **TC-INF-015** — Dapr secret store resolves secrets
  - API can connect to PostgreSQL (DevelopmentSeed runs)
  - API can connect to MinIO (report upload works)
  - API can connect to Keycloak (user provisioning works)

## 1.6 Observability

- [ ] **TC-INF-016** — Logs flow to Grafana
  - Open Grafana → Explore → Loki datasource
  - Query: `{job="nexora"}` → API structured logs visible
  - Verify: log entries contain tenant_id, correlation_id

- [ ] **TC-INF-017** — Traces flow to Tempo
  - Make an API request, note the TraceId from response
  - Grafana → Explore → Tempo → search by TraceId → trace visible
  - Verify: spans include ASP.NET Core, EF Core, HTTP client

- [ ] **TC-INF-018** — Health endpoints respond
  - `GET /health` → 200
  - `GET /health/live` → 200
  - `GET /health/ready` → 200
  - `GET /health/startup` → 200
