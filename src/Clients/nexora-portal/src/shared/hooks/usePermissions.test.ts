import { beforeEach, describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';

import { useAuthStore } from '@/shared/lib/stores/authStore';

import { usePermissions } from './usePermissions';

const mockUser = {
  id: 'u1',
  email: 'test@nexora.io',
  firstName: 'Test',
  lastName: 'User',
  status: 'Active',
  organizations: [],
};

describe('usePermissions', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession();
  });

  it('should return false for any permission when no session is set', () => {
    const { result } = renderHook(() => usePermissions());

    expect(result.current.hasPermission('identity.users.read')).toBe(false);
  });

  it('should return empty permissions array when no session', () => {
    const { result } = renderHook(() => usePermissions());

    expect(result.current.permissions).toEqual([]);
  });

  it('should return true for granted permission', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 't1',
      permissions: ['contacts.contacts.read', 'donations.donations.write'],
    });

    const { result } = renderHook(() => usePermissions());

    expect(result.current.hasPermission('contacts.contacts.read')).toBe(true);
    expect(result.current.hasPermission('donations.donations.write')).toBe(true);
  });

  it('should return false for non-granted permission', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 't1',
      permissions: ['contacts.contacts.read'],
    });

    const { result } = renderHook(() => usePermissions());

    expect(result.current.hasPermission('contacts.contacts.delete')).toBe(false);
  });

  it('should check any permission correctly', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 't1',
      permissions: ['contacts.contacts.read'],
    });

    const { result } = renderHook(() => usePermissions());

    expect(
      result.current.hasAnyPermission([
        'contacts.contacts.read',
        'donations.donations.read',
      ]),
    ).toBe(true);
  });

  it('should return false from hasAnyPermission when none match', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 't1',
      permissions: ['contacts.contacts.read'],
    });

    const { result } = renderHook(() => usePermissions());

    expect(
      result.current.hasAnyPermission([
        'donations.donations.read',
        'crm.leads.read',
      ]),
    ).toBe(false);
  });

  it('should return false from hasAnyPermission with empty array', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 't1',
      permissions: ['contacts.contacts.read'],
    });

    const { result } = renderHook(() => usePermissions());

    expect(result.current.hasAnyPermission([])).toBe(false);
  });
});
