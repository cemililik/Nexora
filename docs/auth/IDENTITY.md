# Nexora - Identity, Authentication & Authorization

## 1. Architecture Overview

```mermaid
---
title: Authentication Architecture
---
flowchart LR
    User["User\n(Browser)"] --> APISIX["APISIX\nGateway"]
    APISIX --> Nexora["Nexora\nAPI"]
    Nexora --> PG[("PostgreSQL\n(tenant schema)")]
    APISIX -- "JWT\nvalidation" --> Keycloak["Keycloak\n(OIDC)"]
    style APISIX fill:#e74c3c,color:#fff
    style Keycloak fill:#4d4d4d,color:#fff
    style PG fill:#336791,color:#fff
```

### Authentication Flow

```mermaid
---
title: OIDC Authentication Flow
---
sequenceDiagram
    actor User
    participant Frontend as Frontend (React/Next.js)
    participant APISIX as APISIX Gateway
    participant KC as Keycloak
    participant API as Nexora API
    participant DB as PostgreSQL

    User->>Frontend: Navigate to app
    Frontend->>KC: Redirect to login (OIDC Auth Code + PKCE)
    KC->>User: Show login page
    User->>KC: Enter credentials
    KC->>Frontend: Authorization code
    Frontend->>KC: Exchange code for tokens
    KC->>Frontend: Access token (JWT) + Refresh token

    User->>Frontend: Perform action
    Frontend->>APISIX: API request + Bearer token
    APISIX->>APISIX: Validate JWT (signature, expiry)
    APISIX->>API: Forward request + validated claims
    API->>API: Extract tenant_id, org_id, roles
    API->>DB: SET search_path = tenant_schema
    API->>DB: Execute query (org_id filter)
    DB->>API: Results
    API->>APISIX: Response
    APISIX->>Frontend: Response
```

## 2. Keycloak Configuration

### Realm Structure
```mermaid
---
title: Keycloak Realm Structure
---
flowchart TB
    Master["Nexora Master Realm\n(Platform Admin Only)"]

    subgraph Realm1["Realm: isabet-group"]
        C1["Client: nexora-admin\n(Public SPA)"]
        C2["Client: nexora-portal\n(Public Portal)"]
        C3["Client: nexora-api\n(Confidential)"]
        Users1["Users"]
        Groups1["Groups → Organizations"]
        Roles1["Roles → Permissions"]
    end

    subgraph Realm2["Realm: another-org"]
        C4["Client: nexora-admin"]
        C5["Client: nexora-portal"]
        C6["Client: nexora-api"]
        Users2["Users"]
    end

    Master --> Realm1
    Master --> Realm2

    style Master fill:#2c3e50,color:#fff
    style Realm1 fill:#3498db,color:#fff
    style Realm2 fill:#27ae60,color:#fff
```

### Why Realm-per-Tenant?
- Complete user isolation between tenants
- Tenant-specific login pages (branding, logo)
- Independent identity provider federation per tenant
- Tenant-specific password policies
- Clean data export/import per tenant

### Client Configuration

| Client | Type | Grant | Use |
|--------|------|-------|-----|
| nexora-admin | Public | Authorization Code + PKCE | Admin SPA login |
| nexora-portal | Public | Authorization Code + PKCE | Portal/donor login |
| nexora-api | Confidential | Client Credentials | Service-to-service |
| nexora-mobile | Public | Authorization Code + PKCE | Mobile app (future) |

### JWT Token Claims
```json
{
  "sub": "user-uuid",
  "iss": "https://auth.nexora.io/realms/isabet-group",
  "aud": "nexora-api",
  "tenant_id": "isabet-group",
  "org_id": "org-uuid",
  "organizations": ["isabet-academy", "ikf"],
  "roles": ["admin", "crm.manager"],
  "permissions": ["crm.leads.read", "crm.leads.write", "donations.view"],
  "exp": 1711111111,
  "iat": 1711107511
}
```

### Permission Resolution (Frontend)
The frontend resolves permissions from two sources with backend priority:

1. **`GET /api/v1/identity/users/me`** — returns `permissions` array loaded from DB (OrganizationUser → UserRole → RolePermission → Permission)
2. **JWT `permissions` claim** — fallback if `/me` fails

```text
Frontend useAuth → api.get('/identity/users/me') → response.permissions
                 ↓ fallback
                 JWT claims.permissions (Keycloak user attribute)
```

This ensures newly added permissions (e.g., audit module) are immediately available after role assignment, without requiring Keycloak token refresh or re-login.

## 3. Multi-Tenancy Identity Model

```mermaid
---
title: Identity Hierarchy
---
flowchart TB
    Platform["Platform Level"]
    Tenant["Tenant\n(Keycloak Realm)"]
    Org1["Organization 1\nIsabet Academy"]
    Org2["Organization 2\nIKF Foundation"]
    DeptA["Department A"]
    DeptB["Department B"]
    DeptC["Department C"]
    UserA["User A\nOrg1: Admin\nOrg2: Viewer"]
    UserB["User B\nOrg1: CRM Manager"]

    Platform --> Tenant
    Tenant --> Org1 & Org2
    Org1 --> DeptA & DeptB
    Org2 --> DeptC
    Tenant --> UserA & UserB
    UserA -.-> Org1 & Org2
    UserB -.-> Org1

    style Platform fill:#2c3e50,color:#fff
    style Tenant fill:#8e44ad,color:#fff
    style Org1 fill:#2980b9,color:#fff
    style Org2 fill:#27ae60,color:#fff
```

### Tenant Resolution
Request → APISIX extracts JWT → `tenant_id` claim → Nexora sets PG schema

Priority chain for tenant resolution:
1. JWT `tenant_id` claim (authenticated requests)
2. `X-Tenant-Id` header (service-to-service, validated against service account)
3. Domain mapping: `isabetacademy.nexora.io` → tenant lookup table

## 4. Authorization Model

### Permission-Based RBAC

```
Permission: {module}.{resource}.{action}

Examples:
- crm.leads.read
- crm.leads.write
- crm.leads.delete
- crm.leads.assign
- donations.donations.read
- donations.donations.create
- donations.campaigns.manage
- contacts.contacts.read
- contacts.contacts.merge
- admin.users.manage
- admin.roles.manage
```

### Role Definition
Roles are **tenant-defined** (not hardcoded). Default roles are seeded but can be customized:

```json
{
  "role": "CRM Manager",
  "organization": "Isabet Academy",
  "permissions": [
    "crm.leads.*",
    "crm.pipeline.*",
    "contacts.contacts.read",
    "contacts.contacts.write",
    "reports.crm.*"
  ]
}
```

### Built-in System Roles (Non-deletable)

| Role | Scope | Description |
|------|-------|-------------|
| Platform Admin | Platform | Manages tenants, modules, system config |
| Tenant Admin | Tenant | Manages all organizations within tenant |
| Org Admin | Organization | Full access within one organization |

### Permission Scopes (Phase 1.5.2)

Permissions have two scopes, controlled by the `PermissionScope` enum on each `Permission` entity (stored as a `VARCHAR(20)` `scope` column in the `identity_permissions` table):

| Scope | Examples | Who can hold |
|-------|----------|--------------|
| `Platform` | `identity.tenants.read`, `identity.tenants.manage` | Platform Admin only |
| `Tenant` | `identity.users.*`, `contacts.contacts.read`, `crm.leads.write` | Tenant admins and users |

**Enforcement rules:**
- `CreateRoleHandler` and `UpdateRoleHandler` reject any permission with `Scope == Platform` when assigning to a tenant role. Returns `lockey_identity_error_platform_permission_denied`.
- `GetPermissionsQuery` accepts an optional `Scope` filter. The tenant admin UI passes `scope=Tenant` so Platform-scope permissions are never shown in role management.
- Idempotent seed migration ensures `identity.tenants.*` permissions are always classified as `Platform` scope even on upgraded databases.

Platform-scope permissions are hidden from the tenant admin UI.
In SaaS mode: managed via NMP. In on-prem mode: managed by the local Platform Admin.

The deployment model is determined by the `DeploymentMode` configuration flag (`SaaS` | `OnPrem`).

**License verification:** `ILicenseVerifier` (SharedKernel) provides a `IsLicensedAsync(tenantId, moduleName)` contract. `NullLicenseVerifier` (Infrastructure) always returns `true` — active until NMP implements real license checks. `platform_license_cache` table in `PlatformDbContext` stores cached license results with `(TenantId, ModuleName)` composite PK.

For full details on license verification and NMP integration, see [MANAGEMENT_PORTAL.md — Integration Points with CRM](../architecture/MANAGEMENT_PORTAL.md#9-integration-points-with-crm).

### Module Endpoint Tenant Isolation (CR-01 / SEC-12)

`ModuleEndpoints` routes were changed from `/api/v1/identity/tenants/{tenantId:guid}/modules` to `/api/v1/identity/tenants/modules`. The tenant ID is now sourced exclusively from the authenticated JWT claim via `ITenantContextAccessor`, eliminating the cross-tenant targeting risk where a malicious caller could supply any `{tenantId}` in the URL.

```http
# Before (SEC-12 finding)
GET /api/v1/identity/tenants/{tenantId:guid}/modules   ← tenant ID from URL

# After (Phase 1.5.2 fix)
GET /api/v1/identity/tenants/modules                   ← tenant ID from JWT claim
```

### Organization-Scoped Access
Users can have different roles in different organizations:
```
User: Ahmet Bey
├── Isabet Academy → Role: "Accountant" (finance.*, reports.finance.*)
└── IKF Foundation → Role: "Donation Manager" (donations.*, reports.donations.*)
```

**Active Organization**: The frontend sets an `X-Organization-Id` header. The API filters all data by this organization. Users can switch organizations in the UI.

## 5. Data Access Control

### Entity-Level Access
```csharp
// Automatic query filter applied by EF Core
modelBuilder.Entity<Lead>()
    .HasQueryFilter(l => l.OrganizationId == _currentOrganization.Id);

// Additional row-level security for sensitive data
modelBuilder.Entity<Donation>()
    .HasQueryFilter(d =>
        d.OrganizationId == _currentOrganization.Id &&
        (_currentUser.HasPermission("donations.donations.read.all") ||
         d.AssignedToUserId == _currentUser.Id));
```

### Shared Resources
Some resources are shared across organizations within a tenant:
- **Contacts**: Visible across orgs (360-degree view), but activities are org-scoped
- **Products**: Configurable (shared or org-specific)
- **Users**: Can belong to multiple orgs

### Cross-Organization Reporting
Users with `reports.consolidated.read` permission can run reports across all organizations they have access to.

## 6. Portal Authentication

### External Users (Donors, Parents, Volunteers)
- Separate Keycloak client (`nexora-portal`)
- Self-registration allowed (with email verification)
- Social login options (Google, Apple — configurable per tenant)
- Limited permissions (only portal-scoped)

### Portal Permissions
```
portal.profile.read
portal.profile.edit
portal.donations.read        (own donations)
portal.donations.create
portal.sponsorships.read      (own sponsorships)
portal.documents.read         (own documents)
portal.appointments.book
```

### Guest Access
Some actions don't require authentication:
- Viewing public donation pages
- Making one-time donations (guest checkout)
- Filling contact/volunteer forms

## 7. Security Measures

### Token Management
- Access token TTL: 5 minutes
- Refresh token TTL: 30 days (with rotation)
- Refresh token rotation: new refresh token on each use, old one invalidated
- Token revocation on: password change, role change, account deactivation

### API Security (APISIX)
- JWT validation (signature, expiry, audience)
- Rate limiting per tenant (prevent noisy neighbor)
- Rate limiting per user (prevent abuse)
- IP allowlisting (optional, for admin APIs)
- Request size limits
- CORS configuration per tenant domain

### Audit Logging
Audit logging is handled by the standalone **Audit module** (`Nexora.Modules.Audit`). It captures all command executions across all modules via the `AuditLogBehavior` MediatR pipeline behavior.

**How it works:**
- `AuditLogBehavior` intercepts all `ICommand` / `ICommand<T>` requests
- Checks `IAuditConfigService` to determine if the operation is enabled for the tenant
- After handler execution, builds an `AuditEntry` with rich context and persists via `IAuditStore`
- Audit failures never block business operations

**Configurable per tenant:**
- Admins with `audit.settings.manage` permission can enable/disable auditing per module and per operation
- Operations are auto-discovered from all registered modules' command types
- Auth events (Login, Logout, PasswordChange, TokenRefresh) are also configurable

**Audit entry fields:**

| Field | Description |
|-------|-------------|
| Module | Source module (e.g., identity, contacts) |
| Operation | Command name (e.g., CreateUser, DeleteContact) |
| OperationType | Create, Update, Delete, or Action |
| UserId / UserEmail | Who performed the action |
| IpAddress | Client IP (via X-Forwarded-For or RemoteIpAddress) |
| UserAgent | Browser/client info |
| CorrelationId | Request trace correlation |
| IsSuccess | Whether the operation succeeded |
| ErrorKey | Localization key if failed |
| EntityType / EntityId | Affected entity |
| BeforeState / AfterState | Entity snapshots (Phase 2) |
| Changes | Field-level diff (Phase 2) |

**Permission model:**

| Permission | Description |
|------------|-------------|
| `audit.logs.read` | View audit log entries |
| `audit.logs.export` | Export audit logs |
| `audit.settings.read` | View audit configuration |
| `audit.settings.manage` | Enable/disable auditing per module/operation |

**Frontend:** Standalone sidebar section "Audit Logs" with log list, detail view, and settings page.

**Storage:** PostgreSQL JSONB in tenant schema (`audit_entries`, `audit_settings`). Future: table partitioning for retention management.

See full spec: [`docs/modules/audit/SPEC.md`](../modules/audit/SPEC.md)

### Secrets Management (Vault)
- DB connection strings
- Keycloak admin credentials
- Payment gateway API keys (Stripe, iyzico)
- SMS/Email provider API keys
- Encryption keys for sensitive data (PII)

### Data Encryption
- At rest: PostgreSQL TDE or column-level encryption for PII
- In transit: TLS 1.3 everywhere
- Sensitive fields (bank details, card tokens): AES-256-GCM via Vault Transit engine

## 8. KVKK / GDPR Compliance

| Requirement | Implementation |
|------------|---------------|
| Consent tracking | Contact model stores consent records with timestamps |
| Right to access | Data export endpoint (user's own data as JSON/CSV) |
| Right to delete | Anonymization process (soft delete + PII removal after retention period) |
| Data portability | Standard export format |
| Breach notification | Audit log + alerting pipeline |
| Data minimization | Only collect what's needed, documented per module |
| Cookie consent | Frontend consent banner, cookie preferences stored per user |
