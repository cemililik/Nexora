import { beforeEach, describe, expect, it } from 'vitest';

import { useAuthStore } from './authStore';

const mockUser = {
  id: 'u1',
  email: 'admin@nexora.io',
  firstName: 'Admin',
  lastName: 'User',
  status: 'Active',
  organizations: [],
};

describe('authStore', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession();
  });

  it('should have empty initial state', () => {
    const state = useAuthStore.getState();
    expect(state.user).toBeNull();
    expect(state.token).toBeNull();
    expect(state.tenantId).toBeNull();
    expect(state.isAuthenticated).toBe(false);
    expect(state.permissions).toEqual([]);
  });

  it('should set session with all fields', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      token: 'jwt-token',
      tenantId: 'tenant-1',
      organizationId: 'org-1',
      permissions: ['identity.users.read', 'identity.users.write'],
    });

    const state = useAuthStore.getState();
    expect(state.user).toEqual(mockUser);
    expect(state.token).toBe('jwt-token');
    expect(state.tenantId).toBe('tenant-1');
    expect(state.organizationId).toBe('org-1');
    expect(state.isAuthenticated).toBe(true);
    expect(state.permissions).toEqual(['identity.users.read', 'identity.users.write']);
  });

  it('should set session with optional organizationId as null', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      token: 'jwt-token',
      tenantId: 'tenant-1',
      permissions: [],
    });

    expect(useAuthStore.getState().organizationId).toBeNull();
  });

  it('should clear session completely', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      token: 'jwt-token',
      tenantId: 'tenant-1',
      organizationId: 'org-1',
      permissions: ['read'],
    });

    useAuthStore.getState().clearSession();

    const state = useAuthStore.getState();
    expect(state.user).toBeNull();
    expect(state.token).toBeNull();
    expect(state.tenantId).toBeNull();
    expect(state.organizationId).toBeNull();
    expect(state.isAuthenticated).toBe(false);
    expect(state.permissions).toEqual([]);
  });

  it('should update token without affecting other state', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      token: 'old-token',
      tenantId: 'tenant-1',
      permissions: ['read'],
    });

    useAuthStore.getState().updateToken('new-token');

    const state = useAuthStore.getState();
    expect(state.token).toBe('new-token');
    expect(state.user).toEqual(mockUser);
    expect(state.isAuthenticated).toBe(true);
  });

  it('should check single permission correctly', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      token: 'jwt-token',
      tenantId: 'tenant-1',
      permissions: ['identity.users.read', 'contacts.contacts.write'],
    });

    expect(useAuthStore.getState().hasPermission('identity.users.read')).toBe(true);
    expect(useAuthStore.getState().hasPermission('identity.users.delete')).toBe(false);
  });

  it('should check any permission correctly', () => {
    useAuthStore.getState().setSession({
      user: mockUser,
      token: 'jwt-token',
      tenantId: 'tenant-1',
      permissions: ['identity.users.read'],
    });

    expect(
      useAuthStore.getState().hasAnyPermission(['identity.users.read', 'identity.users.write']),
    ).toBe(true);
    expect(
      useAuthStore.getState().hasAnyPermission(['identity.users.delete', 'contacts.contacts.write']),
    ).toBe(false);
  });
});
