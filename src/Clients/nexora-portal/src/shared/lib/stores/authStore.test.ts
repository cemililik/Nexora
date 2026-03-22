import { beforeEach, describe, expect, it } from 'vitest';

import type { UserInfo } from '@/shared/types/auth';

import { useAuthStore } from './authStore';

const mockUser: UserInfo = {
  id: '123',
  email: 'test@example.com',
  firstName: 'John',
  lastName: 'Doe',
  status: 'Active',
  organizations: [
    {
      organizationId: 'org-1',
      organizationName: 'Test Org',
      isDefault: true,
    },
  ],
};

describe('authStore', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession();
  });

  it('should start with empty state', () => {
    const state = useAuthStore.getState();
    expect(state.user).toBeNull();
    expect(state.isAuthenticated).toBe(false);
    expect(state.permissions).toEqual([]);
  });

  it('should set session correctly', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 'tenant-1',
      organizationId: 'org-1',
      permissions: ['contacts.contacts.read', 'donations.donations.read'],
    });

    const state = useAuthStore.getState();
    expect(state.user).toEqual(mockUser);
    expect(state.tenantId).toBe('tenant-1');
    expect(state.organizationId).toBe('org-1');
    expect(state.isAuthenticated).toBe(true);
    expect(state.permissions).toHaveLength(2);
  });

  it('should clear session correctly', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 'tenant-1',
      organizationId: 'org-1',
      permissions: ['contacts.contacts.read'],
    });

    useAuthStore.getState().clearSession();

    const state = useAuthStore.getState();
    expect(state.user).toBeNull();
    expect(state.tenantId).toBeNull();
    expect(state.organizationId).toBeNull();
    expect(state.isAuthenticated).toBe(false);
    expect(state.permissions).toEqual([]);
  });

  it('should check single permission correctly', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 'tenant-1',
      permissions: ['contacts.contacts.read', 'donations.donations.write'],
    });

    expect(useAuthStore.getState().hasPermission('contacts.contacts.read')).toBe(true);
    expect(useAuthStore.getState().hasPermission('contacts.contacts.delete')).toBe(false);
  });

  it('should check any permission correctly', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 'tenant-1',
      permissions: ['contacts.contacts.read'],
    });

    expect(
      useAuthStore.getState().hasAnyPermission([
        'contacts.contacts.read',
        'donations.donations.read',
      ]),
    ).toBe(true);

    expect(
      useAuthStore.getState().hasAnyPermission([
        'donations.donations.read',
        'crm.leads.read',
      ]),
    ).toBe(false);
  });

  it('should handle organizationId as optional', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      tenantId: 'tenant-1',
      permissions: [],
    });

    expect(useAuthStore.getState().organizationId).toBeNull();
  });
});
