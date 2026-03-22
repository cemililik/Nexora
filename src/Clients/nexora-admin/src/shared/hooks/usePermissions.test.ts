import { beforeEach, describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';

import { useAuthStore } from '@/shared/lib/stores/authStore';

import { usePermissions } from './usePermissions';

const mockUser = {
  id: 'u1',
  email: 'admin@nexora.io',
  firstName: 'Admin',
  lastName: 'User',
  status: 'Active',
  organizations: [],
};

describe('usePermissions', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession();
  });

  it('should return false when no permissions are set', () => {
    const { result } = renderHook(() => usePermissions());
    expect(result.current.hasPermission('identity.users.read')).toBe(false);
  });

  it('should check specific permission correctly', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      token: 'token',
      tenantId: 't1',
      permissions: ['identity.users.read', 'contacts.contacts.write'],
    });

    const { result } = renderHook(() => usePermissions());
    expect(result.current.hasPermission('identity.users.read')).toBe(true);
    expect(result.current.hasPermission('identity.users.delete')).toBe(false);
  });

  it('should check any permission with partial match', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      token: 'token',
      tenantId: 't1',
      permissions: ['identity.users.read'],
    });

    const { result } = renderHook(() => usePermissions());
    expect(
      result.current.hasAnyPermission(['identity.users.read', 'identity.users.write']),
    ).toBe(true);
    expect(
      result.current.hasAnyPermission(['identity.users.delete']),
    ).toBe(false);
  });
});
