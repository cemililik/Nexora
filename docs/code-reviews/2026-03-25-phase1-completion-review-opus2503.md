# Code Review: Phase 1 Completion — Last 10 Commits

**Reviewer**: Claude Opus 4.6 (AI Agent)
**Date**: 2026-03-25
**Scope**: Commits `22ffe89..3d23b74` (10 commits, 108 files changed)
**Standards Applied**: CODING_STANDARDS.md, OBSERVABILITY_STANDARDS.md, FRONTEND_STANDARDS.md, API_INTEGRATION_STANDARDS.md, CODE_REVIEW_STANDARDS.md, LOCALIZATION_STANDARDS.md

---

## Summary

| Severity | Backend | Frontend | Total |
|----------|---------|----------|-------|
| Critical | 3 | 5 | **8** |
| High | 14 | 9 | **23** |
| Medium | 22 | 17 | **39** |
| Low | 7 | 0 | **7** |
| **Total** | **46** | **31** | **77** |

---

## CRITICAL Issues

### BE-001: SQL Injection Risk in UninstallModuleCommand
- **File**: `src/Modules/Nexora.Modules.Identity/Application/Commands/UninstallModuleCommand.cs`
- **Lines**: 128-133
- **Category**: SEC-5
- **Description**: `RenameModuleTablesAsync` constructs raw SQL using string interpolation with `schemaName` and `moduleName` values. The `table_name LIKE '{prefix}%'` and `ALTER TABLE` commands are built via string concatenation without parameterized queries. `moduleName` is a string from the request and could be crafted maliciously.

### BE-002: SQL Injection Risk in TestReportQueryQuery
- **File**: `src/Modules/Nexora.Modules.Reporting/Application/Queries/TestReportQueryQuery.cs`
- **Lines**: 31
- **Category**: SEC-5
- **Description**: User-supplied `QueryText` is embedded directly into another SQL statement via string interpolation. The `SqlQueryValidator` keyword-blocklist approach can be bypassed with comment injection, Unicode tricks, or PostgreSQL-specific functions (`dblink`, `lo_import`, `pg_read_file`).

### BE-003: SqlQueryValidator Is Insufficiently Robust
- **File**: `src/Modules/Nexora.Modules.Reporting/Infrastructure/Services/SqlQueryValidator.cs`
- **Lines**: 11-57
- **Category**: SEC-5
- **Description**: Missing from blocklist: `SET`, `COMMENT`, `VACUUM`, `REINDEX`, `DO` (PL/pgSQL). SQL comments (`--`, `/* */`) are not stripped before checking. PostgreSQL functions like `dblink`, `lo_import` are not blocked.

### FE-001: Hardcoded User-Facing String — "No permissions assigned"
- **File**: `src/Clients/nexora-admin/src/modules/identity/pages/RoleDetailPage.tsx`
- **Line**: 248
- **Category**: L10N-1
- **Description**: `"No permissions assigned"` is a hardcoded English string. Zero-tolerance violation per LOCALIZATION_STANDARDS.

### FE-002: Hardcoded User-Facing String — "rows"
- **File**: `src/Clients/nexora-admin/src/modules/reporting/pages/ReportDetailPage.tsx`
- **Line**: 182
- **Category**: L10N-1
- **Description**: `{exec.rowCount} rows` contains hardcoded English text.

### FE-003: Hardcoded User-Facing String — "ms"
- **File**: `src/Clients/nexora-admin/src/modules/reporting/pages/ReportDetailPage.tsx`
- **Line**: 183
- **Category**: L10N-1
- **Description**: `{exec.durationMs}ms` contains hardcoded unit suffix.

### FE-004: Hardcoded Placeholder — "contacts"
- **File**: `src/Clients/nexora-admin/src/modules/reporting/pages/ReportListPage.tsx`
- **Line**: 225
- **Category**: L10N-1
- **Description**: `placeholder="contacts"` is a hardcoded user-visible string.

### FE-005: Hardcoded SQL Placeholder
- **File**: `src/Clients/nexora-admin/src/modules/reporting/pages/ReportListPage.tsx`
- **Line**: 277
- **Category**: L10N-1
- **Description**: `placeholder="SELECT id, name FROM contacts_contacts"` is hardcoded.

---

## HIGH Issues

### BE-004: Missing HasFilter on TenantModuleConfiguration Unique Index
- **File**: `src/Modules/Nexora.Modules.Identity/Infrastructure/Configurations/TenantModuleConfiguration.cs`
- **Line**: 19
- **Category**: ARCH-17
- **Description**: Unique index on `{TenantId, ModuleName}` missing `.HasFilter("\"IsDeleted\" = false")`. Will cause constraint violations on reinstall after soft delete.

### BE-005: Missing HasFilter on TenantConfiguration Unique Index
- **File**: `src/Modules/Nexora.Modules.Identity/Infrastructure/Configurations/TenantConfiguration.cs`
- **Line**: 20
- **Category**: ARCH-17
- **Description**: Unique index on `Slug` missing partial filter. PlatformDbContext inline config has it, creating inconsistency.

### BE-006: catch(Exception) in DeleteUserCommand
- **File**: `src/Modules/Nexora.Modules.Identity/Application/Commands/DeleteUserCommand.cs`
- **Lines**: 64-68
- **Category**: OBS-7
- **Description**: Generic `Exception` caught and swallowed for Keycloak call. Forbidden in module code per observability standards.

### BE-007: catch(Exception) in ActivateModuleCommand
- **File**: `src/Modules/Nexora.Modules.Identity/Application/Commands/ActivateModuleCommand.cs`
- **Lines**: 57-62
- **Category**: OBS-7

### BE-008: catch(Exception) in UninstallModuleCommand
- **File**: `src/Modules/Nexora.Modules.Identity/Application/Commands/UninstallModuleCommand.cs`
- **Lines**: 160-165
- **Category**: OBS-7

### BE-009: catch(Exception) in TestReportQueryQuery
- **File**: `src/Modules/Nexora.Modules.Reporting/Application/Queries/TestReportQueryQuery.cs`
- **Lines**: 44-48
- **Category**: OBS-7
- **Description**: Additionally, `ex.Message` leaked to client as localization parameter — information disclosure risk.

### BE-010: Missing Warning Log — DeactivateModuleCommand
- **File**: `src/Modules/Nexora.Modules.Identity/Application/Commands/DeactivateModuleCommand.cs`
- **Lines**: 44-45
- **Category**: OBS-3

### BE-011: Missing Warning Log — UpdateRoleCommand (IsSystemRole check)
- **File**: `src/Modules/Nexora.Modules.Identity/Application/Commands/UpdateRoleCommand.cs`
- **Lines**: 51-53
- **Category**: OBS-3

### BE-012: Missing Warning Log — DeleteRoleCommand (IsSystemRole + assignedCount)
- **File**: `src/Modules/Nexora.Modules.Identity/Application/Commands/DeleteRoleCommand.cs`
- **Lines**: 43-44, 48-50
- **Category**: OBS-3

### BE-013: Result.Failure with Raw String — UninstallModuleCommand
- **File**: `src/Modules/Nexora.Modules.Identity/Application/Commands/UninstallModuleCommand.cs`
- **Line**: 58
- **Category**: L10N-4
- **Description**: `Result.Failure("lockey_...")` instead of `Result.Failure(LocalizedMessage.Of("lockey_..."))`.

### BE-014: Result.Failure with Raw String — UpdateTenantCommand
- **File**: `src/Modules/Nexora.Modules.Identity/Application/Commands/UpdateTenantCommand.cs`
- **Line**: 53
- **Category**: L10N-4

### FE-006: Missing onError on useCreateUser, useUpdateProfile, useUpdateUserStatus
- **File**: `src/Clients/nexora-admin/src/modules/identity/hooks/useUsers.ts`
- **Lines**: 49-57, 63-73, 76-90
- **Category**: API-11

### FE-007: Missing onError on all module management hooks
- **File**: `src/Clients/nexora-admin/src/modules/identity/hooks/useModuleManagement.ts`
- **Lines**: 23-90
- **Category**: API-11

### FE-008: Direct API Call Bypassing TanStack Query
- **File**: `src/Clients/nexora-admin/src/modules/identity/pages/UserDetailPage.tsx`
- **Lines**: 308-318
- **Category**: ARCH-5
- **Description**: `AddToOrgDialog.handleAdd` calls `api.post(...)` directly instead of using a `useMutation` hook. Does not invalidate queries after success.

### FE-009: Missing Permission Checks — RoleDetailPage
- **File**: `src/Clients/nexora-admin/src/modules/identity/pages/RoleDetailPage.tsx`
- **Lines**: 21-222
- **Category**: SEC-9
- **Description**: Edit/delete buttons shown without checking `identity.roles.update`/`identity.roles.delete` permissions.

### FE-010: Missing Permission Checks — ReportDetailPage
- **File**: `src/Clients/nexora-admin/src/modules/reporting/pages/ReportDetailPage.tsx`
- **Lines**: 139-148
- **Category**: SEC-9

### FE-011: Missing Permission Checks — ReportListPage
- **File**: `src/Clients/nexora-admin/src/modules/reporting/pages/ReportListPage.tsx`
- **Line**: 102
- **Category**: SEC-9

### FE-012: Portal api.delete Crashes on 204 Responses
- **File**: `src/Clients/nexora-portal/src/shared/lib/api.ts`
- **Lines**: 81-84
- **Category**: API-7
- **Description**: `unwrapEnvelope` throws if `data.data` is undefined, but 204 responses have no body. Admin version correctly handles this.

### FE-013: Missing loading.tsx and error.tsx for Portal Routes
- **File**: Portal dashboard and profile route directories
- **Category**: ARCH-14
- **Description**: New Next.js route segments (dashboard, profile) lack co-located loading and error files.

### FE-014: Mid-File Import Statement
- **File**: `src/Clients/nexora-admin/src/modules/identity/pages/UserDetailPage.tsx`
- **Line**: 248
- **Category**: CQ-2
- **Description**: Import statement appears mid-file after the main component export.

---

## MEDIUM Issues

### Backend (22 issues)

| # | File | Line(s) | Category | Description |
|---|------|---------|----------|-------------|
| BE-015 | UpdateRoleCommand.cs | 14 | CQ-4 | Missing XML documentation on public record |
| BE-016 | DeleteRoleCommand.cs | 13,15,23 | CQ-4 | Missing XML docs on 3 public types |
| BE-017 | DeleteUserCommand.cs | 15,17,25 | CQ-4 | Missing XML docs on 3 public types |
| BE-018 | AssignUserRolesCommand.cs | 14,19,29 | CQ-4 | Missing XML docs on 3 public types |
| BE-019 | ActivateModuleCommand.cs | 13,15 | CQ-4 | Missing XML docs on 2 public types |
| BE-020 | DeactivateModuleCommand.cs | 12,14 | CQ-4 | Missing XML docs on 2 public types |
| BE-021 | GetRoleByIdQuery.cs | 12,24 | CQ-4 | Missing XML docs on 2 public types |
| BE-022 | GetUserRolesQuery.cs | 12,14 | CQ-4 | Missing XML docs on 2 public types |
| BE-023 | UpdateRoleCommand.cs | 20,30 | CQ-4 | Missing XML docs on validator and handler |
| BE-024 | Multiple Reporting files | various | CQ-4 | Missing XML docs across 6 files |
| BE-025 | TestReportQueryQuery.cs | 47 | SEC-2 | `ex.Message` leaked as client-facing param |
| BE-026 | CreateReportDefinitionCommand.cs | 48 | L10N-2 | Validator error string passed to client |
| BE-027 | ActivateModuleCommand.cs | 47 | OBS-3 | Missing Warning log (already active) |
| BE-028 | DeleteRoleCommand.cs | 48-50 | OBS-3 | Missing Warning log (role has users) |
| BE-029 | DeleteUserCommand.cs | 49-50 | OBS-3 | Missing Warning log (self-delete) |
| BE-030 | AssignUserRolesCommand.cs | 60-62 | OBS-3 | Missing Warning log (invalid roles) |
| BE-031 | GetRoleByIdQuery.cs | 38-40 | OBS-4 | Missing Debug log for not-found |
| BE-032 | GetUserRolesQuery.cs | 29-31 | OBS-4 | Missing Debug log for not-found |
| BE-033 | PlatformDbContext.cs | 30 | ARCH-7 | DeletedBy always null (no user resolution) |
| BE-034 | GetReportFileQuery.cs | 39 | CFG-1 | Hardcoded bucket name "nexora-reports" |
| BE-035 | SqlQueryValidator.cs | 23-51 | L10N-2 | Hardcoded English error strings |
| BE-036 | UninstallModuleCommand.cs | 110 | CQ-2 | `new LocalizedMessage()` instead of `LocalizedMessage.Of()` |

### Frontend (17 issues)

| # | File | Line(s) | Category | Description |
|---|------|---------|----------|-------------|
| FE-015 | PermissionSelector.tsx | 76 | L10N-1 | Raw module name displayed without translation |
| FE-016 | RoleDetailPage.tsx | 234 | L10N-1 | Raw module name in PermissionReadOnly |
| FE-017 | TenantDetailPage.tsx | 305 | L10N-1 | Raw module name in InstallModuleDialog |
| FE-018 | TenantDetailPage.tsx | 131 | L10N-1 | Raw module name in module list |
| FE-019 | ReportDetailPage.tsx, ReportListPage.tsx | — | CQ-3 | Missing breadcrumb setup (inconsistent) |
| FE-020 | UserDetailPage.tsx | 424-436 | A11Y | Checkboxes lack explicit id/htmlFor pairs |
| FE-021 | PermissionSelector.tsx | 67-89 | A11Y | Checkboxes lack aria-label |
| FE-022 | RoleDetailPage.tsx | 83-87 | A11Y | Edit mode Input lacks label association |
| FE-023 | RoleDetailPage.tsx | 129-131 | A11Y | Description Input lacks id/htmlFor |
| FE-024 | SqlTestResult.tsx | 36 | PERF | Array index used as React key |
| FE-025 | RoleDetailPage.tsx | 55,67 | PERF | Missing useCallback on handlers |
| FE-026 | ReportDetailPage.tsx | 67-93 | PERF | useCallback with unstable dependency |
| FE-027 | Portal api.ts | 54 | TS-2 | Loose null check (`=== undefined` vs `== null`) |
| FE-028 | useAuth.test.ts | — | TEST | Missing test for store-preserved unauthenticated path |
| FE-029 | Reporting types/index.ts | 14,21,25,91 | TS-2 | Loose string types where union types exist |
| FE-030 | Identity types/index.ts | 55 | TS-2 | `RoleDto.permissions: string[]` vs `RoleDetailDto.permissions: PermissionDto[]` inconsistency |
| FE-031 | Reporting components | — | CQ-3 | No test files co-located with SqlEditor/SqlTestResult |

---

## LOW Issues (7)

| # | File | Category | Description |
|---|------|----------|-------------|
| BE-037 | RoleEndpoints.cs | CQ-2 | Multiple DTOs in same file |
| BE-038 | UserEndpoints.cs | CQ-2 | Three DTOs in same file |
| BE-039 | UpdateRoleCommand.cs | PERF | Full permissions table loaded into memory |
| BE-040 | GetUserRolesQuery.cs | PERF | Same full table load |
| BE-041 | UpdateTenantStatusTests.cs | TEST | Missing FluentAssertions explicit import |
| BE-042 | UpdateTenantStatusTests.cs | TEST | Terminate test doesn't verify module deactivation |
| BE-043 | AuditableEntity.cs | CQ-2 | Asymmetric access modifiers (internal vs public) |

---

## Verdict

**Do NOT merge** until Critical and High issues are resolved.

Priority order:
1. **SQL injection** (BE-001, BE-002, BE-003) — immediate security risk
2. **Missing HasFilter** (BE-004, BE-005) — data integrity risk
3. **Hardcoded strings** (FE-001 through FE-005) — zero-tolerance policy
4. **Missing onError** (FE-006, FE-007) — error swallowing
5. **Permission checks** (FE-009, FE-010, FE-011) — authorization bypass
6. **catch(Exception)** (BE-006 through BE-009) — observability violation
7. **Missing logs** (BE-010 through BE-012) — observability violation
