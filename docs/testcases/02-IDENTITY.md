# Suite 02 — Identity & Access Management

**Prerequisite:** Suite 01 passed (infrastructure healthy)
**Execution Order:** 2
**Base URL:** `http://localhost:9080/api/v1/identity`

---

## 2.1 Authentication

- [ ] **TC-IDN-001** — Login via Keycloak returns valid JWT
  - Navigate to Admin Dashboard `http://localhost:3001`
  - Click login → redirected to Keycloak
  - Enter `platformadmin@nexora.dev` / `Admin123!`
  - Verify: redirected back to admin with valid session
  - Verify: JWT contains `tenant_id`, `organization_id`, `sub` claims

- [ ] **TC-IDN-002** — Unauthenticated request returns 401
  - `GET /users/me` without Authorization header
  - Expected: 401 Unauthorized

- [ ] **TC-IDN-003** — Expired token returns 401
  - Send request with a manually expired JWT
  - Expected: 401 Unauthorized

## 2.2 Current User

- [ ] **TC-IDN-004** — Get current user (/me)
  - `GET /users/me` with valid token
  - Expected: 200 with user object (id, firstName, lastName, email)
  - Verify: email matches `platformadmin@nexora.dev`

## 2.3 User Management

- [ ] **TC-IDN-005** — List users
  - `GET /users?page=1&pageSize=10`
  - Expected: 200 with paginated list, at least 1 user (platform admin)

- [ ] **TC-IDN-006** — Create user
  - `POST /users` with `{ email: "testuser@nexora.dev", firstName: "Test", lastName: "User", temporaryPassword: "Test1234!" }`
  - Expected: 201 Created with user object
  - Verify: user appears in Keycloak `nexora-dev` realm

- [ ] **TC-IDN-007** — Create user with duplicate email fails
  - `POST /users` with same email as TC-IDN-006
  - Expected: 400 Bad Request with validation error

- [ ] **TC-IDN-008** — Create user with invalid email fails
  - `POST /users` with `{ email: "not-an-email" }`
  - Expected: 400 with validation error on email field

- [ ] **TC-IDN-009** — Get user by ID
  - `GET /users/{id}` using ID from TC-IDN-006
  - Expected: 200 with user details including organization memberships

- [ ] **TC-IDN-010** — Update user profile
  - `PUT /users/{id}/profile` with `{ firstName: "Updated", lastName: "Name", phoneNumber: "+905551234567" }`
  - Expected: 200
  - Verify: Keycloak user profile also updated

- [ ] **TC-IDN-011** — Deactivate user
  - `PUT /users/{id}/status` with `{ action: "Deactivate" }`
  - Expected: 200
  - Verify: user disabled in Keycloak

- [ ] **TC-IDN-012** — Activate user
  - `PUT /users/{id}/status` with `{ action: "Activate" }`
  - Expected: 200
  - Verify: user enabled in Keycloak

## 2.4 Role & Permission Management

- [ ] **TC-IDN-013** — List permissions
  - `GET /permissions`
  - Expected: 200 with 63 permissions
  - Verify: permissions follow `module.resource.action` format

- [ ] **TC-IDN-014** — List permissions filtered by module
  - `GET /permissions?module=contacts`
  - Expected: 200 with only contacts module permissions (21)

- [ ] **TC-IDN-015** — List roles
  - `GET /roles`
  - Expected: 200 with at least "Platform Admin" role

- [ ] **TC-IDN-016** — Create role with permissions
  - `POST /roles` with `{ name: "Report Viewer", permissionIds: [<reporting.definition.read>, <reporting.execution.read>] }`
  - Expected: 201 with role object containing assigned permissions

## 2.5 Organization Management

- [ ] **TC-IDN-017** — List organizations
  - `GET /organizations?page=1&pageSize=10`
  - Expected: 200 with at least 1 organization (dev org)

- [ ] **TC-IDN-018** — Create organization
  - `POST /organizations` with `{ name: "Test Org", timezone: "Europe/Istanbul", defaultCurrency: "TRY", defaultLanguage: "tr" }`
  - Expected: 201 with organization object

- [ ] **TC-IDN-019** — Get organization by ID
  - `GET /organizations/{id}` using ID from TC-IDN-018
  - Expected: 200 with full details including member count

- [ ] **TC-IDN-020** — Update organization
  - `PUT /organizations/{id}` with `{ name: "Updated Org", timezone: "UTC", defaultCurrency: "USD", defaultLanguage: "en" }`
  - Expected: 200 with updated data

- [ ] **TC-IDN-021** — Add member to organization
  - `POST /organizations/{id}/members` with `{ userId: "<user-from-TC-IDN-006>" }`
  - Expected: 200
  - Verify: member count incremented

- [ ] **TC-IDN-022** — List organization members
  - `GET /organizations/{id}/members?page=1&pageSize=10`
  - Expected: 200 with paginated member list

- [ ] **TC-IDN-023** — Remove member from organization
  - `DELETE /organizations/{id}/members/{userId}`
  - Expected: 200
  - Verify: member count decremented

- [ ] **TC-IDN-024** — Delete organization
  - `DELETE /organizations/{id}` (using test org from TC-IDN-018)
  - Expected: 200 (soft delete)

## 2.6 Tenant Management

- [ ] **TC-IDN-025** — List tenants
  - `GET /tenants?page=1&pageSize=10`
  - Expected: 200 with at least 1 tenant (dev tenant)

- [ ] **TC-IDN-026** — Get tenant by ID
  - `GET /tenants/{dev-tenant-id}`
  - Expected: 200 with tenant details

## 2.7 Module Management

- [ ] **TC-IDN-027** — List installed modules
  - `GET /tenants/{tenantId}/modules`
  - Expected: 200 with list of installed modules (identity, contacts, documents, notifications, reporting)

- [ ] **TC-IDN-028** — Install module
  - `POST /tenants/{tenantId}/modules` with `{ moduleName: "crm" }` (if not already installed)
  - Expected: 201 or appropriate response
  - Note: May fail if CRM module code doesn't exist yet — document expected behavior

- [ ] **TC-IDN-029** — Uninstall module
  - `DELETE /tenants/{tenantId}/modules/crm` (if installed in TC-IDN-028)
  - Expected: 204 No Content

## 2.8 Audit Logs

- [ ] **TC-IDN-030** — Audit log records exist
  - `GET /audit-logs?page=1&pageSize=10`
  - Expected: 200 with audit entries from previous test operations

- [ ] **TC-IDN-031** — Filter audit logs by user
  - `GET /audit-logs?userId={admin-user-id}`
  - Expected: 200 with filtered results

- [ ] **TC-IDN-032** — Filter audit logs by date range
  - `GET /audit-logs?from=2026-03-01&to=2026-03-31`
  - Expected: 200 with filtered results
