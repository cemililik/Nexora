import { create } from 'zustand';

import type { UserInfo } from '@/shared/types/auth';

interface AuthState {
  user: UserInfo | null;
  token: string | null;
  tenantId: string | null;
  organizationId: string | null;
  permissions: string[];
  isAuthenticated: boolean;

  setSession: (params: {
    user: UserInfo;
    token: string;
    tenantId: string;
    organizationId?: string;
    permissions: string[];
  }) => void;
  clearSession: () => void;
  updateToken: (token: string) => void;
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
    }),

  updateToken: (token: string) => set({ token }),

  hasPermission: (permission: string) =>
    get().permissions.includes(permission),

  hasAnyPermission: (permissions: string[]) =>
    permissions.some((p) => get().permissions.includes(p)),
}));
