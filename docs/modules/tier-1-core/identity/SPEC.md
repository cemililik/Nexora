# Module: Identity & Access Management

**Tier:** 1 — Platform Core

> **Status**: Implemented
> **Version**: 1.0.0
> **Module Name**: `identity`
> **Tier**: Core/Platform (always installed)
> **Dependencies**: None (foundational)

## Overview

The Identity module is the **foundational module** of Nexora. It manages multi-tenancy, organizations, users, roles, and permissions. Every other module depends on it for authentication context, tenant resolution, and authorization checks. It integrates with Keycloak as the external identity provider and provides the internal permission/role engine.

## Domain Model

### Entities

```mermaid
---
title: Identity Module - Entity Relationship Diagram
---
erDiagram
    Tenant ||--o{ Organization : "has many"
    Tenant ||--o{ TenantModule : "has installed"
    Tenant {
        uuid id PK
        string name
        string slug UK
        string realm_id
        string status
        jsonb settings
        timestamp created_at
    }

    Organization ||--o{ Department : "has many"
    Organization ||--o{ OrganizationUser : "has members"
    Organization {
        uuid id PK
        uuid tenant_id FK
        string name
        string slug UK
        string logo_url
        string timezone
        string default_currency
        string default_language
        boolean is_active
    }

    Department {
        uuid id PK
        uuid organization_id FK
        uuid parent_department_id FK
        string name
        boolean is_active
    }

    User ||--o{ OrganizationUser : "belongs to"
    User {
        uuid id PK
        uuid tenant_id FK
        string keycloak_user_id UK
        string email UK
        string first_name
        string last_name
        string phone
        string avatar_url
        string status
        timestamp last_login_at
    }

    OrganizationUser ||--o{ UserRole : "has roles"
    OrganizationUser {
        uuid id PK
        uuid user_id FK
        uuid organization_id FK
        boolean is_default_org
    }

    Role ||--o{ RolePermission : "has many"
    Role ||--o{ UserRole : "assigned to"
    Role {
        uuid id PK
        uuid tenant_id FK
        string name
        string description
        boolean is_system_role
        boolean is_active
    }

    UserRole {
        uuid id PK
        uuid organization_user_id FK
        uuid role_id FK
    }

    Permission ||--o{ RolePermission : "granted in"
    Permission {
        uuid id PK
        string module
        string resource
        string action
        string description
    }

    RolePermission {
        uuid id PK
        uuid role_id FK
        uuid permission_id FK
    }

    TenantModule {
        uuid id PK
        uuid tenant_id FK
        string module_name
        string version
        string status
        timestamp installed_at
    }

```

> **Note**: Audit logging has moved to the standalone **Audit module** (`Nexora.Modules.Audit`).
> The `AuditLog` entity previously defined here is no longer part of the Identity module.
> See [`docs/modules/audit/SPEC.md`](../audit/SPEC.md) for the current audit logging design.

### Value Objects

| Value Object | Description |
|-------------|-------------|
| `TenantId` | Strongly-typed tenant identifier |
| `OrganizationId` | Strongly-typed organization identifier |
| `UserId` | Strongly-typed user identifier |
| `Email` | Validated email address |
| `TenantSlug` | URL-safe tenant identifier (used in domain mapping) |
| `PermissionKey` | `{module}.{resource}.{action}` format string |

### Domain Events

| Event | Trigger | Consumers |
|-------|---------|-----------|
| `TenantCreated` | New tenant registered | Infrastructure (create PG schema, Keycloak realm) |
| `TenantSuspended` | Admin suspends tenant | All modules (disable access) |
| `OrganizationCreated` | New org within tenant | Contacts (create default address book), Finance (create default journal) |
| `UserCreated` | New user registered | Notifications (send welcome email), Contacts (link or create contact) |
| `UserDeactivated` | User account disabled | All modules (revoke sessions) |
| `RoleAssigned` | Role given to user | Audit log |
| `RoleRevoked` | Role removed from user | Audit log |
| `ModuleInstalled` | Module activated for tenant | Module (run initialization, seed data) |
| `ModuleUninstalled` | Module deactivated | Module (cleanup, archive data) |

### Entity Lifecycles

```mermaid
---
title: Tenant Lifecycle
---
stateDiagram-v2
    [*] --> Trial: Register tenant
    Trial --> Active: Activate (payment confirmed)
    Trial --> Terminated: Cancel before activation
    Active --> Suspended: Admin suspends
    Active --> Terminated: Admin terminates
    Active --> Active: Update settings (no-op)
    Suspended --> Active: Admin reactivates
    Suspended --> Terminated: Admin terminates
    Terminated --> [*]
```

```mermaid
---
title: User Lifecycle
---
stateDiagram-v2
    [*] --> Invited: Admin invites
    Invited --> Active: User accepts & sets password
    Invited --> Expired: Invitation timeout (72h)
    Expired --> Invited: Resend invitation
    Active --> Suspended: Admin suspends
    Active --> Active: Update profile / roles
    Suspended --> Active: Admin reactivates
    Suspended --> Deactivated: After retention period
    Deactivated --> [*]
```

### Sequence Diagrams

```mermaid
---
title: User Login Flow
---
sequenceDiagram
    participant User
    participant Frontend
    participant Keycloak
    participant APISIX as APISIX Gateway
    participant API as Identity API
    participant Cache as Redis Cache
    participant DB as PostgreSQL

    User->>Frontend: Enter credentials
    Frontend->>Keycloak: POST /realms/{tenant}/protocol/openid-connect/token
    Keycloak-->>Frontend: JWT (access_token + refresh_token)
    Frontend->>APISIX: GET /api/v1/identity/users/me (Bearer token)
    APISIX->>APISIX: Validate JWT signature & expiry
    APISIX->>API: Forward validated request
    API->>API: Extract tenant_id, user_id from JWT claims
    API->>Cache: Get user permissions (identity:permissions:{userId})
    alt Cache hit
        Cache-->>API: Cached permissions
    else Cache miss
        API->>DB: Load user roles + permissions for org
        DB-->>API: Permission set
        API->>Cache: Store permissions (TTL 15min)
    end
    API-->>Frontend: ApiEnvelope<UserProfileDto> (user + permissions)
    Frontend->>Frontend: Store user context, render dashboard
```

```mermaid
---
title: Create User Flow
---
sequenceDiagram
    participant Admin
    participant API as Identity API
    participant Validator as FluentValidation
    participant Handler as InviteUserHandler
    participant Keycloak
    participant DB as PostgreSQL
    participant Kafka

    Admin->>API: POST /api/v1/identity/users/invite {email, name, roles}
    API->>Validator: Validate command
    Validator-->>API: Validation passed
    API->>Handler: Send(InviteUserCommand)
    Handler->>DB: Check if user exists in tenant
    alt New user
        Handler->>Keycloak: Create user in tenant realm
        Keycloak-->>Handler: Keycloak user ID
        Handler->>DB: Insert User (status: Invited, keycloak_user_id)
        Handler->>DB: Insert OrganizationUser + UserRole records
    else Existing user
        Handler->>DB: Add OrganizationUser + UserRole for new org
    end
    Handler->>Kafka: Publish UserCreated event
    Handler-->>API: Result.Success(UserDto)
    API-->>Admin: ApiEnvelope<UserDto>

    Note over Kafka: Notifications module picks up event,<br/>sends invitation email
```

```mermaid
---
title: Role Assignment Flow
---
sequenceDiagram
    participant Admin
    participant API as Identity API
    participant Handler as AssignRoleHandler
    participant DB as PostgreSQL
    participant Cache as Redis Cache
    participant Kafka

    Admin->>API: POST /api/v1/identity/users/{userId}/roles {roleId}
    API->>Handler: Send(AssignRoleCommand)
    Handler->>DB: Verify user exists and belongs to org
    Handler->>DB: Verify role exists and is active
    Handler->>DB: Check role not already assigned
    Handler->>DB: Insert UserRole record
    Handler->>Cache: Invalidate identity:permissions:{userId}
    Handler->>Kafka: Publish RoleAssigned event
    Handler-->>API: Result.Success
    API-->>Admin: ApiEnvelope (success)

    Note over Cache: Next request from user will<br/>reload permissions from DB
```

### Component Diagram

```mermaid
---
title: Identity Module - Component Diagram
---
flowchart TD
    subgraph Api["Api Layer"]
        TE[TenantEndpoints]
        OE[OrganizationEndpoints]
        UE[UserEndpoints]
        RE[RoleEndpoints]
        PE[PermissionEndpoints]
        ALE[AuditLogEndpoints<br/>deprecated adapter → Audit module]
    end

    subgraph Application["Application Layer"]
        CMD[Commands<br/>CreateTenant, InviteUser,<br/>AssignRole, SuspendUser, ...]
        QRY[Queries<br/>GetUser, ListRoles,<br/>GetPermissions, ...]
        VAL[Validators<br/>FluentValidation per command]
        DTO[DTOs<br/>UserDto, RoleDto,<br/>TenantDto, ...]
    end

    subgraph Domain["Domain Layer"]
        ENT[Entities<br/>Tenant, Organization, User,<br/>Role, Permission, ...]
        VO[Value Objects<br/>TenantId, UserId, Email,<br/>PermissionKey, ...]
        EVT[Domain Events<br/>UserCreated, RoleAssigned,<br/>TenantSuspended, ...]
    end

    subgraph Infrastructure["Infrastructure Layer"]
        DBC[IdentityDbContext<br/>EF Core]
        REPO[Repositories]
        KCS[KeycloakService<br/>User & Realm sync]
        CACHE[CacheService<br/>Permission & Tenant cache]
    end

    subgraph External["External Services"]
        KC[Keycloak<br/>Identity Provider]
        PG[(PostgreSQL)]
        RD[(Redis)]
    end

    Api --> Application
    Application --> Domain
    Application --> Infrastructure
    Infrastructure --> External

    KCS --> KC
    DBC --> PG
    CACHE --> RD
```

### Integration Diagram

```mermaid
---
title: Identity Module - Integration Diagram
---
flowchart LR
    subgraph Clients
        AdminUI[nexora-admin<br/>React 19]
        Portal[nexora-portal<br/>Next.js 16]
    end

    subgraph Gateway
        APISIX[APISIX<br/>JWT Validation]
    end

    subgraph Identity["Identity Module"]
        IAPI[Identity API]
        IApp[Application Layer<br/>CQRS Handlers]
        IDomain[Domain Layer]
        IInfra[Infrastructure Layer]
    end

    subgraph ExternalAuth["External Auth"]
        KC[Keycloak<br/>Realm-per-tenant<br/>OIDC/JWT]
    end

    subgraph SharedInfra["Shared Infrastructure"]
        PG[(PostgreSQL<br/>public + tenant schemas)]
        Redis[(Redis<br/>Permission cache)]
        Kafka[Kafka<br/>Event bus]
    end

    subgraph ConsumerModules["Consumer Modules"]
        Contacts[Contacts Module]
        CRM[CRM Module]
        Notifications[Notifications Module]
        Audit[Audit Module]
        Documents[Documents Module]
    end

    Clients -->|HTTPS| APISIX
    APISIX -->|Validated request| IAPI
    Clients -->|Auth| KC
    IInfra -->|User sync, realm mgmt| KC
    IInfra --> PG
    IInfra --> Redis
    IApp -->|Publish events| Kafka

    Kafka -->|UserCreated| Contacts
    Kafka -->|UserCreated| Notifications
    Kafka -->|RoleAssigned| Audit
    Kafka -->|TenantCreated| CRM
    Kafka -->|ModuleInstalled| ConsumerModules
```

## Use Cases

### UC-IDN-001: Create Tenant

- **Actor**: Platform Admin
- **Preconditions**: Admin is authenticated with platform admin role
- **Flow**:
  1. Admin provides tenant name, slug, admin email
  2. System validates slug uniqueness
  3. System creates tenant record (status: Provisioning)
  4. System creates PostgreSQL schema for tenant
  5. System creates Keycloak realm for tenant
  6. System creates initial admin user in Keycloak
  7. System seeds default roles and permissions
  8. System installs default modules (Identity, Contacts)
  9. Tenant status → Active
  10. System sends welcome email to admin
- **Postconditions**: Tenant is active with schema, realm, and admin user
- **Business Rules**:
  - Slug must be unique, URL-safe, 3-50 characters
  - Admin email must be unique across platform
- **Exceptions**:
  - Schema creation fails → rollback, status → Failed, alert platform admin
  - Keycloak realm creation fails → rollback schema, status → Failed

### UC-IDN-002: Invite User

- **Actor**: Org Admin
- **Preconditions**: Actor has `admin.users.manage` permission for the organization
- **Flow**:
  1. Admin provides email, name, organization, roles
  2. System checks if user already exists in tenant
  3. If new: create user record (status: Invited), create Keycloak user, send invitation email
  4. If existing: add organization membership and roles, send notification
- **Postconditions**: User has pending invitation or new org membership
- **Business Rules**:
  - Maximum 1 invitation per email per 24 hours
  - Invitation expires after 72 hours
  - User can belong to multiple organizations with different roles

### UC-IDN-003: Resolve Tenant & Authorize Request

- **Actor**: System (middleware)
- **Preconditions**: Request has valid JWT
- **Flow**:
  1. Extract `tenant_id` from JWT claims
  2. Lookup tenant in cache (Redis) or DB
  3. Verify tenant is Active
  4. Set PostgreSQL `search_path` to tenant schema
  5. Extract `org_id` from request header (`X-Organization-Id`)
  6. Verify user has membership in requested organization
  7. Load user permissions for this organization (cached in Redis)
  8. Set current context (tenant, org, user, permissions)
- **Postconditions**: Request context is fully resolved, downstream code has access to tenant/org/user
- **Business Rules**:
  - Suspended tenants: return 403
  - No org membership: return 403
  - Missing org header: use user's default organization

### UC-IDN-004: Manage Roles & Permissions

- **Actor**: Tenant Admin
- **Preconditions**: Actor has `admin.roles.manage` permission
- **Flow**:
  1. Admin creates/edits role with name and permission set
  2. System validates permissions exist and are from installed modules
  3. System saves role
  4. If editing: invalidate permission cache for all users with this role
- **Business Rules**:
  - System roles (Platform Admin, Tenant Admin, Org Admin) cannot be deleted or have permissions removed
  - Custom roles can use wildcard permissions: `crm.*`, `crm.leads.*`
  - Permission changes take effect on next request (cache invalidation)

### UC-IDN-005: Switch Organization

- **Actor**: User (with multi-org access)
- **Preconditions**: User is authenticated
- **Flow**:
  1. User selects target organization from dropdown
  2. Frontend sets `X-Organization-Id` header on subsequent requests
  3. API resolves new org context
  4. UI refreshes with organization-specific data
- **Business Rules**:
  - User can only switch to organizations they are a member of
  - Permissions change based on target organization's roles

## API Endpoints

### Tenant Management (Platform Admin)

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/v1/identity/tenants` | Create tenant | `platform.tenants.create` |
| GET | `/api/v1/identity/tenants` | List tenants | `platform.tenants.read` |
| GET | `/api/v1/identity/tenants/{id}` | Get tenant details | `platform.tenants.read` |
| PUT | `/api/v1/identity/tenants/{id}` | Update tenant | `platform.tenants.update` |
| POST | `/api/v1/identity/tenants/{id}/suspend` | Suspend tenant | `platform.tenants.manage` |
| POST | `/api/v1/identity/tenants/{id}/activate` | Activate tenant | `platform.tenants.manage` |
| GET | `/api/v1/identity/tenants/{id}/modules` | List installed modules | `platform.tenants.read` |
| POST | `/api/v1/identity/tenants/{id}/modules` | Install module | `identity.modules.manage` |
| DELETE | `/api/v1/identity/tenants/{id}/modules/{name}?cascade={bool}` | Uninstall module (T-026: refuses with `lockey_identity_error_module_uninstall_blocked_by_dependent` when an installed module declares the target in `Dependencies` and `cascade=false`; with `cascade=true` uninstalls the dependent subtree in reverse-dependency order under per-module transactions and a session `pg_advisory_lock` per ADR-0031) | `identity.modules.manage` |

### Organization Management

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/v1/identity/organizations` | Create organization | `admin.organizations.create` |
| GET | `/api/v1/identity/organizations` | List organizations | `admin.organizations.read` |
| GET | `/api/v1/identity/organizations/{id}` | Get organization | `admin.organizations.read` |
| PUT | `/api/v1/identity/organizations/{id}` | Update organization | `admin.organizations.update` |
| GET | `/api/v1/identity/organizations/{id}/members` | List members | `admin.users.read` |

### User Management

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/v1/identity/users/invite` | Invite user | `admin.users.manage` |
| GET | `/api/v1/identity/users` | List users | `admin.users.read` |
| GET | `/api/v1/identity/users/{id}` | Get user details | `admin.users.read` |
| PUT | `/api/v1/identity/users/{id}` | Update user | `admin.users.manage` |
| POST | `/api/v1/identity/users/{id}/suspend` | Suspend user | `admin.users.manage` |
| POST | `/api/v1/identity/users/{id}/activate` | Activate user | `admin.users.manage` |
| GET | `/api/v1/identity/users/me` | Get current user | Authenticated |
| PUT | `/api/v1/identity/users/me` | Update own profile | Authenticated |
| GET | `/api/v1/identity/users/me/organizations` | List my organizations | Authenticated |

### Role & Permission Management

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/v1/identity/roles` | Create role | `admin.roles.manage` |
| GET | `/api/v1/identity/roles` | List roles | `admin.roles.read` |
| PUT | `/api/v1/identity/roles/{id}` | Update role | `admin.roles.manage` |
| DELETE | `/api/v1/identity/roles/{id}` | Delete role (returns 200 OK with ApiEnvelope) | `admin.roles.manage` |
| GET | `/api/v1/identity/permissions` | List all permissions | `admin.roles.read` |
| POST | `/api/v1/identity/users/{id}/roles` | Assign role to user | `admin.users.manage` |
| DELETE | `/api/v1/identity/users/{id}/roles/{roleId}` | Revoke role (returns 200 OK with ApiEnvelope) | `admin.users.manage` |

### Audit Log

> **Deprecated**: Audit logging endpoints have moved to the standalone **Audit module** (`/api/v1/audit/*`).
> See [`docs/modules/audit/SPEC.md`](../audit/SPEC.md) for the current API.

## Integration Points

### Events Produced

| Event | Topic | Description |
|-------|-------|-------------|
| `identity.tenant.created` | `nexora.identity.tenants` | New tenant provisioned |
| `identity.tenant.suspended` | `nexora.identity.tenants` | Tenant suspended |
| `identity.organization.created` | `nexora.identity.organizations` | New org created |
| `identity.user.created` | `nexora.identity.users` | New user registered |
| `identity.user.deactivated` | `nexora.identity.users` | User deactivated |
| `identity.role.changed` | `nexora.identity.roles` | Role permissions updated |
| `identity.user.roles_changed` (`UserRolesChangedIntegrationEvent`) | `nexora.identity.roles` | User's role assignment changed — triggers permission cache invalidation (inline at handler + event-driven cross-instance via Kafka) |
| `identity.module.installed` (`ModuleInstalledIntegrationEvent`) | `nexora.identity.modules` | Module installed for tenant |
| `identity.module.uninstalled` (`ModuleUninstalledIntegrationEvent`) | `nexora.identity.modules` | Module uninstalled from tenant — extended schema (T-026 / ADR-0028): `CanonicalTableNames`, `RenamedTableNames`. Per-event UTC timestamp on `IntegrationEventBase.OccurredAt`. One row per per-module step inside a cascade. |
| `identity.module.uninstall_failed` (`ModuleUninstallFailedIntegrationEvent`) | `nexora.identity.modules` | Cascade-uninstall sequence failed partway through (T-026 / ADR-0031). Carries `TargetModuleName`, `FailedModuleName`, `ErrorLockey`, and `SuccessfulModulesSoFar` (the forward log). Earlier modules in the log remain uninstalled — operators reinstall within retention to recover. |

**Cascade event consumer contract** (T-026):

- **Idempotency.** Consumers of `identity.module.uninstalled` MUST dedupe on the tuple `(TenantIdGuid, ModuleName, OccurredAt)` — the cascade orchestrator emits one row per module step, and Hangfire's at-least-once delivery means a consumer can see the same `(tenant, module)` event again on retry. The OutboxService's `EventId` is the canonical de-dup key for inbox-pattern handlers (see `IInboxGuard`); the (tenant, module, OccurredAt) tuple is the secondary defense for handlers that don't use `IInboxGuard`.
- **Ordering.** Within a single cascade run the events are emitted in reverse-dependency order (deepest dependent first, target last) by virtue of the orchestrator's loop; the outbox publishes them in the order they were committed. **Cross-cascade ordering is NOT guaranteed** — two operators uninstalling different subtrees may interleave on the broker. Consumers that need to reason about a specific cascade as a unit should group by `(TargetModuleName, OccurredAt-window)` rather than relying on broker-level ordering.
- **`ModuleUninstallFailedIntegrationEvent` reaction.** `SuccessfulModulesSoFar` is a **forward log**, not an instruction. Consumers SHOULD: (a) record the partial state (admin UI, audit), (b) alert if the failure crosses an SLO threshold, and (c) NOT auto-reinstall — operator action is the recovery primitive (reinstall within retention restores the renamed `_del_` tables; cleanup happens via T-025's purge job after the retention window).
| `identity.user.contact_linked` (`UserContactLinkedIntegrationEvent`) | `nexora.identity.users` | Admin linked a user to a Contacts record |
| `identity.user.contact_unlinked` (`UserContactUnlinkedIntegrationEvent`) | `nexora.identity.users` | User↔Contact link removed (`Reason = "manual" | "gdpr_erasure"`) |

### Permission Cache Invalidation

When a user's roles change (assign/revoke), permission cache is invalidated through two mechanisms:
1. **Inline**: The command handler directly calls `ICacheService.RemoveAsync()` for the affected user's permission cache key
2. **Event-driven**: `UserRolesChangedIntegrationEvent` is published via Outbox → Kafka, and all application instances invalidate the user's cached permissions, ensuring cross-instance consistency

### Events Consumed

| Event | Source | Action |
|-------|--------|--------|
| `ContactGdprDeletedIntegrationEvent` | Contacts | Nulls `User.ContactId` on every user in the event's tenant whose `ContactId` matches the erased contact; emits `UserContactUnlinkedIntegrationEvent` (`Reason = "gdpr_erasure"`) per unlinked user. Idempotent via `InboxGuard<IdentityDbContext>`. |

## §User↔Contact Linking

Admins can link a human `User` to a Contacts module `Contact` record for a 360° view
(e.g. a staff member who is also a donor). The link is always initiated by a person;
system/service accounts are rejected.

**FK shape.** `User.ContactId` is a raw `Guid?` (not the Contacts `ContactId` value
object) — Identity and Contacts are both Tier-1 modules and must not cross-reference
each other's internal types. The column is nullable, indexed
(`ix_identity_users_contact_id`), and has no FK constraint at the DB level
(schema-per-tenant with separate module DbContexts).

**Permission.** `identity.users.link_contact` (tenant scope). Seeded by
`IdentityModuleMigration` and granted to the Platform Admin role by default. Gates both
`POST /users/{id}/link-contact` and `DELETE /users/{id}/link-contact`.

**System-account rejection.** `User.IsSystemAccount` is a computed property that
checks whether `KeycloakUserId` begins with `service-account-` (Keycloak's convention
for client service accounts). `User.LinkContact(...)` throws a `DomainException` with
key `lockey_identity_user_link_contact_system_account_rejected`; the command handler
surfaces this as `Result.Failure(...)` before the entity is touched.

**Link flow.** `LinkUserContactCommand` loads the user, validates preconditions
(tenant scope, not a system account, not already linked), invokes `User.LinkContact`,
and enqueues `UserContactLinkedIntegrationEvent` via the outbox. The outbox row, the
`ContactId` write, and the `UserContactLinkedDomainEvent` are persisted atomically
through the caller's `SaveChangesAsync`.

**Unlink flow — manual.** `UnlinkUserContactCommand` is idempotent: a no-op when the
user has no link. When there is a link, it clears `ContactId`, raises
`UserContactUnlinkedDomainEvent`, and enqueues `UserContactUnlinkedIntegrationEvent`
with `Reason = "manual"`.

**Unlink flow — GDPR erasure.** `ContactGdprDeletedIntegrationEventHandler` consumes
`ContactGdprDeletedIntegrationEvent` emitted by the Contacts module. Scoped strictly
to `event.TenantId`, it loads all users whose `ContactId` matches the erased contact,
calls `User.UnlinkContact()` on each, and emits a `UserContactUnlinkedIntegrationEvent`
per user with `Reason = "gdpr_erasure"`. Wrapped in `InboxGuard<IdentityDbContext>`
for exactly-once consumer semantics under at-least-once delivery.

### Dependencies

- **Keycloak**: User authentication, realm management, token issuance
- **Redis**: Permission cache, tenant cache, session management
- **PostgreSQL**: Tenant registry (public schema), tenant data (tenant schemas)
- **Kafka**: Event publishing (via Dapr pub/sub)

## Non-Functional Requirements

| Requirement | Target |
|------------|--------|
| Tenant resolution latency | < 5ms (cached) |
| Permission check latency | < 2ms (cached) |
| Max tenants per instance | 5,000 |
| Max users per tenant | 50,000 |
| Max organizations per tenant | 100 |
| Audit log retention | 2 years minimum |
| Cache invalidation | < 1 second after change |
| Invitation email delivery | < 30 seconds |
