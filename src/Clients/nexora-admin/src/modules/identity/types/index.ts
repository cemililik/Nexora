/** User summary from GET /identity/users. */
export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phone?: string;
  status: UserStatus;
  lastLoginAt?: string;
}

/** User detail from GET /identity/users/:id. */
export interface UserDetailDto extends UserDto {
  organizations: UserOrganizationDto[];
}

/** Organization membership within a user response. */
export interface UserOrganizationDto {
  organizationId: string;
  organizationName: string;
  isDefault: boolean;
}

/** Request body for POST /identity/users. */
export interface CreateUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  temporaryPassword: string;
}

/** Request body for PUT /identity/users/:id/profile. */
export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  phone?: string;
}

/** Request body for PUT /identity/users/:id/status. */
export interface UpdateUserStatusRequest {
  action: 'activate' | 'deactivate';
}

export type UserStatus = 'Active' | 'Inactive' | 'Locked';

// ─── Roles ──────────────────────────────────────────────────

/** Role from GET /identity/roles. */
export interface RoleDto {
  id: string;
  name: string;
  description?: string;
  isSystemRole: boolean;
  isActive: boolean;
  permissions: string[];
  createdAt: string;
}

/** Request body for POST /identity/roles. */
export interface CreateRoleRequest {
  name: string;
  description?: string;
  permissionIds?: string[];
}

/** Request body for PUT /identity/roles/:id. */
export interface UpdateRoleRequest {
  name: string;
  description?: string;
  permissionIds?: string[];
}

/** Role detail from GET /identity/roles/:id. */
export interface RoleDetailDto {
  id: string;
  name: string;
  description?: string;
  isSystemRole: boolean;
  isActive: boolean;
  permissions: PermissionDto[];
  assignedUserCount: number;
}

/** Request body for PUT /identity/users/:id/roles. */
export interface AssignRolesRequest {
  organizationId: string;
  roleIds: string[];
}

/** User assigned to a role from GET /identity/roles/:id/users. */
export interface RoleUserDto {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  organizationId: string;
  organizationName: string;
}

// ─── Permissions ────────────────────────────────────────────

/** Permission from GET /identity/permissions. */
export interface PermissionDto {
  id: string;
  module: string;
  resource: string;
  action: string;
  key: string;
  description?: string;
}

// ─── Tenants ────────────────────────────────────────────────

/** Tenant summary from GET /identity/tenants. */
export interface TenantDto {
  id: string;
  name: string;
  slug: string;
  status: TenantStatus;
  realmId?: string;
  createdAt: string;
}

/** Tenant detail from GET /identity/tenants/:id. */
export interface TenantDetailDto extends TenantDto {
  settings?: string;
  installedModules: string[];
}

/** Request body for POST /identity/tenants. */
export interface CreateTenantRequest {
  name: string;
  slug: string;
}

/** Request body for PUT /identity/tenants/:id/status. */
export interface UpdateTenantStatusRequest {
  action: 'activate' | 'suspend' | 'terminate';
}

export type TenantStatus = 'Trial' | 'Active' | 'Suspended' | 'Terminated';

// ─── Organizations ──────────────────────────────────────────

/** Organization summary from GET /identity/organizations. */
export interface OrganizationDto {
  id: string;
  name: string;
  slug: string;
  logoUrl?: string;
  timezone: string;
  defaultCurrency: string;
  defaultLanguage: string;
  isActive: boolean;
}

/** Organization detail from GET /identity/organizations/:id. */
export interface OrganizationDetailDto extends OrganizationDto {
  memberCount: number;
}

/** Request body for POST /identity/organizations. */
export interface CreateOrganizationRequest {
  name: string;
  slug: string;
}

/** Request body for PUT /identity/organizations/:id. */
export interface UpdateOrganizationRequest {
  name: string;
  timezone: string;
  defaultCurrency: string;
  defaultLanguage: string;
}

/** Organization member from GET /identity/organizations/:id/members. */
export interface OrganizationMemberDto {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  isDefaultOrg: boolean;
  joinedAt: string;
}

/** Request body for POST /identity/organizations/:id/members. */
export interface AddMemberRequest {
  userId: string;
  isDefault?: boolean;
}

// ─── Audit Logs ─────────────────────────────────────────────

/** Audit log entry from GET /identity/audit-logs. */
export interface AuditLogDto {
  id: string;
  userId: string;
  action: string;
  ipAddress?: string;
  userAgent?: string;
  timestamp: string;
  details?: string;
}

/** Query params for audit log filtering. */
export interface AuditLogFilterParams {
  userId?: string;
  action?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}
