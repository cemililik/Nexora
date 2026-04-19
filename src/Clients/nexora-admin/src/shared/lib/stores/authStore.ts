import { create } from 'zustand';

import type { UserInfo } from '@/shared/types/auth';

export type AuthToken = string | { error: string } | null;

/** Tenant-level regional settings loaded once at login. */
export interface TenantLocale {
  /** IETF locale for number/date formatting (e.g. "en-US", "tr-TR"). */
  locale: string;
  /** ISO 4217 currency code (e.g. "USD", "TRY"). */
  currency: string;
  /** IANA timezone identifier (e.g. "UTC", "Europe/Istanbul"). */
  timezone: string;
  /** BCP 47 language for document/PDF rendering (e.g. "en", "tr"). */
  documentLanguage: string;
}

interface AuthState {
  user: UserInfo | null;
  token: AuthToken;
  tenantId: string | null;
  organizationId: string | null;
  permissions: string[];
  isAuthenticated: boolean;
  /** Tenant regional settings — null until populated after login. */
  tenantLocale: TenantLocale | null;

  setSession: (params: {
    user: UserInfo;
    token: string;
    tenantId: string;
    organizationId?: string;
    permissions: string[];
  }) => void;
  clearSession: () => void;
  updateToken: (token: string) => void;
  setTenantLocale: (locale: TenantLocale) => void;
  hasPermission: (permission: string) => boolean;
  hasAnyPermission: (permissions: string[]) => boolean;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  token: null,
  tenantId: null,
  organizationId: null,
  permissions: [],
  isAuthenticated: false,
  tenantLocale: null,

  setSession: ({ user, token, tenantId, organizationId, permissions }) =>
    set({
      user,
      token,
      tenantId,
      organizationId: organizationId ?? null,
      permissions,
      isAuthenticated: true,
    }),

  clearSession: () =>
    set({
      user: null,
      token: null,
      tenantId: null,
      organizationId: null,
      permissions: [],
      isAuthenticated: false,
      tenantLocale: null,
    }),

  updateToken: (token: string) => set({ token }),

  setTenantLocale: (locale: TenantLocale) => set({ tenantLocale: locale }),

  hasPermission: (permission: string) =>
    get().permissions.includes(permission),

  hasAnyPermission: (permissions: string[]) =>
    permissions.some((p) => get().permissions.includes(p)),
}));
