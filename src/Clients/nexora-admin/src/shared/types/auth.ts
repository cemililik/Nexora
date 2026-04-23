/** Current user info from GET /api/v1/identity/users/me. */
export interface UserInfo {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phone?: string;
  status: string;
  lastLoginAt?: string;
  organizations: UserOrganization[];
  permissions?: string[];
  preferredLanguage?: string;
}

/** Organization membership within a user response. */
export interface UserOrganization {
  organizationId: string;
  organizationName: string;
  isDefault: boolean;
}

/** Organization detail with branding fields. */
export interface OrganizationBranding {
  id: string;
  name: string;
  slug: string;
  logoUrl: string | null;
  timezone: string;
  defaultCurrency: string;
  defaultLanguage: string;
  isActive: boolean;
  memberCount: number;
}

/** Mirrors backend AuthEventType enum — values sent to POST /audit/events/auth. */
export const AuthEventType = {
  Login: 'Login',
  Logout: 'Logout',
  PasswordChange: 'PasswordChange',
  TokenRefresh: 'TokenRefresh',
  LoginFailed: 'LoginFailed',
} as const;
export type AuthEventType = (typeof AuthEventType)[keyof typeof AuthEventType];

/** JWT claims extracted from Keycloak token. */
export interface JwtClaims {
  sub: string;
  tenant_id: string;
  organization_id?: string;
  permissions: string[];
  preferred_username: string;
  email: string;
  exp: number;
}
