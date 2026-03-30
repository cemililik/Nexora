# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Audit module: standalone audit logging with configurable settings per module/operation
- Documents module: folder access control with time-limited permissions
- Documents module: drag-and-drop file upload with presigned URL flow
- Documents module: template variable definitions and rendering
- Platform job architecture for cross-tenant recurring jobs
- Permission-based authorization infrastructure
- JWT claim typed extension methods (ClaimsPrincipalExtensions)

### Changed
- DaprCacheService: fixed value-type caching bug for GetOrSetAsync
- AuditCacheKeys: removed double tenant ID prefix from cache keys
- FolderAccess: DateTime → DateTimeOffset for ExpiresAt
- All Documents query handlers: added AsNoTracking for read-only queries
- All Documents queries: added OrganizationId isolation filter

### Fixed
- Keycloak user delete: GET→modify→PUT full representation
- LastLoginAt connection pool corruption: await instead of fire-and-forget
- API unwrapEnvelope null data handling for void responses
- Audit config case mismatch (Identity vs identity)
