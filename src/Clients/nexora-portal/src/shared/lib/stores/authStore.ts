import { create } from 'zustand';

import type { UserInfo } from '@/shared/types/auth';

interface AuthState {
  user: UserInfo | null;
  tenantId: string | null;
  organizationId: string | null;
  permissions: string[];
  isAuthenticated: boolean;

  setSession: (params: {
    user: UserInfo;
    tenantId: string;
    organizationId?: string;
    permissions: string[];
  }) => void;
  clearSession: () => void;
  hasPermission: (permission: string) => boolean;
  hasAnyPermission: (permissions: string[]) => boolean;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  tenantId: null,
  organizationId: null,
  permissions: [],
  isAuthenticated: false,

  setSession: ({ user, tenantId, organizationId, permissions }) =>
    set({
      user,
      tenantId,
      organizationId: organizationId ?? null,
      permissions,
      isAuthenticated: true,
    }),

  clearSession: () =>
    set({
      user: null,
      tenantId: null,
      organizationId: null,
      permissions: [],
      isAuthenticated: false,
    }),

  hasPermission: (permission: string) =>
    get().permissions.includes(permission),

  hasAnyPermission: (permissions: string[]) =>
    permissions.some((p) => get().permissions.includes(p)),
}));
