# ADR-012: Tenant Management & Authorization Decisions

## Status

Accepted

## Date

2026-03-31

## Context

Nexora is a multi-tenant platform where tenant isolation is critical. Several architectural decisions were needed regarding how tenants are identified, how authorization works, and how tenant management will evolve with the Nexora Management Portal (NMP).

Key questions:
1. Where should tenant ID come from — JWT claims or route parameters?
2. How should endpoint-level authorization work — authentication-only or permission-based?
3. What happens to tenant CRUD in the admin panel when NMP launches?
4. How should platform-level vs tenant-level permissions be separated?

## Decision

### 1. Tenant ID Source: JWT Claims via ITenantContextAccessor

Tenant ID is sourced from JWT `tenant_id` claim, extracted by `TenantMiddleware` and set on `ITenantContextAccessor`. This ensures:
- Tenant isolation cannot be bypassed by URL manipulation
- All downstream code uses the same tenant context
- Schema-per-tenant DB routing is automatic

**Exception (CR-01, deferred to Phase 1.5.2):** `ModuleEndpoints` currently accepts `tenantId` from route parameter (`/tenants/{tenantId:guid}/modules`). This is a platform admin operation that requires the planned `PermissionScope` enum to differentiate "platform operator managing any tenant" from "tenant user managing their own tenant."

### 2. Permission-Based Authorization Infrastructure

Replaced bare `.RequireAuthorization()` (authentication-only) with granular permission policies:

- `PermissionPolicyProvider` — dynamically creates policies from `{module}.{resource}.{action}` strings
- `PermissionAuthorizationHandler` — loads user permissions via `UserPermissionService`
- `UserPermissionService` — queries OrganizationUser → UserRole → RolePermission → Permission chain, cached 5min
- All identity + audit endpoints now enforce specific permissions (e.g., `identity.users.read`, `audit.settings.manage`)

**Pipeline order:** `UseAuthentication()` → `UseMiddleware<TenantMiddleware>()` → `UseAuthorization()` — tenant context must be set before authorization handler runs.

### 3. NMP Replaces Admin Panel Tenant CRUD

No further investment in admin panel tenant management UI. NMP (Nexora Management Portal) will replace:
- Tenant list/create/detail pages
- Module install/uninstall from tenant detail
- Tenant status management

The admin panel retains user, role, organization, and module-level features within a single tenant context.

### 4. Platform vs Tenant Permission Scope (Planned: Phase 1.5.2)

`PermissionScope` enum (`Platform` | `Tenant`) will be added to the Permission entity:
- **Platform scope:** `identity.tenants.manage`, `identity.modules.manage` — Nexora staff (SaaS) or local Platform Admin (on-prem)
- **Tenant scope:** `identity.users.read`, `contacts.contact.write` — tenant admins and users
- Platform-scope permissions hidden from tenant admin UI

## Consequences

### Positive
- Tenant isolation enforced at middleware level — cannot be bypassed
- Granular authorization with cached permissions — low latency
- Clean separation planned between platform and tenant operations
- NMP will provide purpose-built tenant management UX

### Negative
- CR-01 (route-based tenant ID) deferred — `ModuleEndpoints` remains a temporary exception
- Permission cache has up to 5min staleness (mitigated by inline invalidation on role changes)
- `TenantMiddleware` before `UseAuthorization()` adds one middleware hop to every request

## Related

- [ADR-006: Permission-Based Authorization](ADR-006-permission-based-authorization.md)
- [MANAGEMENT_PORTAL.md](../architecture/MANAGEMENT_PORTAL.md)
- Phase 1.5.2: Tenant Permission Isolation (ROADMAP.md)
