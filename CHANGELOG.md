# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Phase 1.5.6 — Permission seed consolidation (ADR-004 enforcement, T-020).**
  New `IPermissionRegistry` interface in SharedKernel + singleton `InMemoryPermissionRegistry`
  in Infrastructure. `IModule.OnStartupAsync(IPermissionRegistry, CancellationToken)` — every
  module now declares its own permissions in its own `OnStartupAsync` (Identity, Contacts,
  Documents, Notifications, Reporting, Audit). `IdentityModuleMigration.SeedAsync` reads from
  the registry instead of a hardcoded cross-module list. `DevelopmentSeed.CreateDefaultPermissions()`
  deleted — dev tenant delegates to `IdentityModuleMigration.SeedAsync`. Role seed now enforces
  `permissions.md` §2 scope rule: tenant-side "Platform Admin" role is **only** granted
  `PermissionScope.Tenant` permissions; any pre-existing Platform-scope assignments are
  automatically stripped on next startup (idempotent). New `Role.RemovePermissionsByIds`
  bulk-revoke API.

- **Phase 1.5.6 — Organization-scoped compliance config with platform caps (ADR-0025, T-019).**
  Three-tier resolver (platform cap → tenant default → org override) replacing tenant-global
  reads for compliance-sensitive keys. New `IConfigurationResolver` interface in SharedKernel,
  `DatabaseConfigurationResolver` in Infrastructure, `NullComplianceCapProvider` (SaaS swaps to
  `NmpComplianceCapProvider` in NMP.2). New permission `contacts.gdpr.settings_manage` (tenant
  scope) separates policy toggling from erasure execution. Platform-scope permission
  `platform.compliance.policy_manage` reserved for NMP. New tenant-schema tables:
  `platform_org_config`, `platform_compliance_policy_audit`. Admin panel compliance settings page
  at `/identity/settings/compliance`. `gdpr.hard_delete.enabled` now resolves per org; runtime
  behaviour of the anonymize vs. hard-delete branch is unchanged (ADR-008 amended). Wire format
  for NMP → CRM caps documented in `docs/architecture/MANAGEMENT_PORTAL.md`.
- Transactional Outbox pattern with per-module DbContext atomicity (OutboxService<TContext>)
- Inbox pattern for idempotent event consumption (InboxGuard<TContext>)
- OutboxProcessor BackgroundService (polling-based, configurable)
- Email/SMS delivery via Kafka (NotificationDeliveryRequestedIntegrationEvent)
- 5 new integration events: UserRolesChanged, ContactImportCompleted, ReportExecuted, FolderAccessGranted/Revoked, ModuleInstalled/Uninstalled
- Cache cross-instance invalidation via Dapr pub/sub
- Outbox monitoring: health check, OpenTelemetry metrics, admin status API
- OutboxCleanupJob (7d retention) and InboxCleanupJob (30d retention)
- ContactGdprDeletedIntegrationEvent for cross-module PII cleanup
- ADR-005 through ADR-012 documenting architectural decisions
- Permission cache invalidation on role changes (inline + event-driven)
- Audit module: standalone audit logging with configurable settings per module/operation
- Documents module: folder access control with time-limited permissions
- Documents module: drag-and-drop file upload with presigned URL flow
- Documents module: template variable definitions and rendering
- Platform job architecture for cross-tenant recurring jobs
- Permission-based authorization infrastructure
- JWT claim typed extension methods (ClaimsPrincipalExtensions)

### Changed

- OutboxProcessor: reflection caching, immediate retry on full batch, tenant-schema iteration
- FileDropZone: progress/isUploading props wired to callers
- OrganizationListPage, RoleListPage: server-side/URL-backed search
- ContactDetailPage: useUnsavedChangesGuard tracks actual form dirty state
- DataTable: controlled page-jump, select-all aria-label
- DashboardPage: uses useAuditLogs hook instead of inline API call
- DaprCacheService: fixed value-type caching bug for GetOrSetAsync
- AuditCacheKeys: removed double tenant ID prefix from cache keys
- FolderAccess: DateTime → DateTimeOffset for ExpiresAt
- All Documents query handlers: added AsNoTracking for read-only queries
- All Documents queries: added OrganizationId isolation filter

### Fixed

- OutboxService atomicity: messages saved in caller's transaction (not separate)
- ConfirmDialog race conditions in RoleDetailPage, DocumentDetailPage
- Redundant isPending checks in ContactDetailPage tabs
- GDPR export: IgnoreQueryFilters on all related entity queries
- InboxCleanupJob SQL injection schema validation
- Keycloak user delete: GET→modify→PUT full representation
- LastLoginAt connection pool corruption: await instead of fire-and-forget
- API unwrapEnvelope null data handling for void responses
- Audit config case mismatch (Identity vs identity)
